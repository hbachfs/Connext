using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Connext.Api.Hubs;

/// <summary>
/// Mọi thông báo realtime gửi qua group cá nhân "user-{id}" (không dùng group theo hội thoại),
/// nên hội thoại tạo SAU khi user đã kết nối vẫn nhận tin ngay, không cần reconnect.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private const int MaxContentLength = 4000;
    private static readonly HashSet<string> AllowedTypes = new() { "text", "image", "file" };

    // Theo dõi các kết nối của mỗi User (1 User có thể mở nhiều tab/trình duyệt)
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    private static readonly object _presenceLock = new();

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ChatHub(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public static string UserGroup(Guid userId) => $"user-{userId}";
    private static bool IsOnline(Guid userId) => _userConnections.ContainsKey(userId);

    private Guid GetCurrentUserId() => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<List<Guid>> GetFriendIdsAsync(Guid userId) =>
        _context.Friendships.AsNoTracking()
            .Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        bool firstConnection;
        lock (_presenceLock)
        {
            if (!_userConnections.TryGetValue(userId, out var set))
            {
                set = new HashSet<string>();
                _userConnections[userId] = set;
            }
            set.Add(Context.ConnectionId);
            firstConnection = set.Count == 1;
        }

        if (firstConnection)
        {
            await _context.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsOnline, true));

            // Chỉ báo trạng thái online cho BẠN BÈ (không phát cho toàn hệ thống)
            var friendGroups = (await GetFriendIdsAsync(userId)).Select(UserGroup).ToList();
            if (friendGroups.Count > 0)
                await Clients.Groups(friendGroups).SendAsync("UserOnlineChanged", userId, true, null);

            await MarkPendingAsDeliveredAsync(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();

        var lastConnection = false;
        lock (_presenceLock)
        {
            if (_userConnections.TryGetValue(userId, out var set))
            {
                set.Remove(Context.ConnectionId);
                if (set.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    lastConnection = true;
                }
            }
        }

        if (lastConnection)
        {
            var lastSeen = DateTime.UtcNow;
            await _context.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsOnline, false).SetProperty(u => u.LastSeenAt, (DateTime?)lastSeen));

            var friendGroups = (await GetFriendIdsAsync(userId)).Select(UserGroup).ToList();
            if (friendGroups.Count > 0)
                await Clients.Groups(friendGroups).SendAsync("UserOnlineChanged", userId, false, lastSeen);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Tin gửi lúc user offline: khi họ online lại thì chuyển Sent -> Delivered và báo cho người gửi.
    private async Task MarkPendingAsDeliveredAsync(Guid userId)
    {
        var pending = _context.Messages.Where(m =>
            m.SenderId != userId && m.Status == "Sent" &&
            _context.ConversationMembers.Any(cm => cm.ConversationId == m.ConversationId && cm.UserId == userId));

        var rows = await pending.Select(m => new { m.ConversationId, m.SenderId }).Distinct().ToListAsync();
        if (rows.Count == 0) return;

        await pending.ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "Delivered"));
        foreach (var r in rows)
            await Clients.Group(UserGroup(r.SenderId)).SendAsync("MessagesDelivered", r.ConversationId, userId);
    }

    // 1. Client gọi để gửi tin nhắn
    public async Task SendMessage(Guid conversationId, string type, string? content, string? fileUrl, string? fileName)
    {
        var userId = GetCurrentUserId();

        // ---- Kiểm tra dữ liệu đầu vào ----
        type = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(type)) throw new HubException("Loại tin nhắn không hợp lệ.");

        content = content?.Trim();
        if (content is { Length: > MaxContentLength }) throw new HubException($"Tin nhắn tối đa {MaxContentLength} ký tự.");

        long? fileSize = null;
        if (type == "text")
        {
            if (string.IsNullOrEmpty(content)) throw new HubException("Tin nhắn không được để trống.");
            fileUrl = null;
            fileName = null;
        }
        else
        {
            fileSize = GetUploadSize(fileUrl) ?? throw new HubException("File đính kèm không hợp lệ.");
            fileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(fileUrl!) : fileName.Trim();
            if (fileName.Length > 255) fileName = fileName[..255];
        }

        // ---- Kiểm tra quyền ----
        var members = await _context.ConversationMembers.Where(cm => cm.ConversationId == conversationId).ToListAsync();
        if (members.All(m => m.UserId != userId)) throw new HubException("Bạn không thuộc hội thoại này.");

        var isGroup = await _context.Conversations.Where(c => c.Id == conversationId).Select(c => c.IsGroup).FirstAsync();
        if (!isGroup)
        {
            var otherId = members.FirstOrDefault(m => m.UserId != userId)?.UserId
                          ?? throw new HubException("Hội thoại không hợp lệ.");
            var areFriends = await _context.Friendships.AsNoTracking().AnyAsync(f => f.Status == "Accepted" &&
                ((f.RequesterId == userId && f.AddresseeId == otherId) || (f.RequesterId == otherId && f.AddresseeId == userId)));
            if (!areFriends) throw new HubException("Hai bạn không còn là bạn bè nên không thể nhắn tin.");
        }

        // ---- Lưu DB ----
        var delivered = members.Any(m => m.UserId != userId && IsOnline(m.UserId));
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Type = type,
            Content = content,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            Status = delivered ? "Delivered" : "Sent"
        };
        _context.Messages.Add(message);

        // Có tin mới thì hội thoại hiện lại trong danh sách của mọi người.
        // KHÔNG đụng tới ClearedAt: lịch sử đã xóa không được hiện lại.
        foreach (var m in members) m.IsHidden = false;

        await _context.SaveChangesAsync();

        // ---- Phát realtime tới từng thành viên (kể cả các tab khác của người gửi) ----
        foreach (var m in members)
            await Clients.Group(UserGroup(m.UserId)).SendAsync("ReceiveMessage", ToDto(message, m.UserId == userId));

        // Người nhận đang online => báo cho người gửi là đã tới nơi
        if (delivered)
            await Clients.Group(UserGroup(userId)).SendAsync("MessageDelivered", conversationId, message.Id);
    }

    // 2. Client gọi khi mở hội thoại để đánh dấu Đã xem
    public async Task MarkAsRead(Guid conversationId)
    {
        var userId = GetCurrentUserId();

        var member = await _context.ConversationMembers
            .FirstOrDefaultAsync(cm => cm.UserId == userId && cm.ConversationId == conversationId);
        if (member == null) return;

        var lastMsgId = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => (long?)m.Id);
        if (lastMsgId == null || lastMsgId <= member.LastReadMessageId) return;

        member.LastReadMessageId = lastMsgId.Value;
        await _context.SaveChangesAsync();

        // Lưu "Seen" vào DB bằng 1 câu lệnh duy nhất => tải lại trang vẫn thấy đúng trạng thái
        await _context.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && m.Id <= lastMsgId && m.Status != "Seen")
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "Seen"));

        var memberIds = await _context.ConversationMembers.AsNoTracking()
            .Where(cm => cm.ConversationId == conversationId).Select(cm => cm.UserId).ToListAsync();
        await Clients.Groups(memberIds.Select(UserGroup).ToList())
            .SendAsync("MessagesSeen", conversationId, userId, lastMsgId.Value);
    }

    // 3. (Tùy chọn) Hiệu ứng "đang nhập..."
    public async Task Typing(Guid conversationId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        var memberIds = await _context.ConversationMembers.AsNoTracking()
            .Where(cm => cm.ConversationId == conversationId).Select(cm => cm.UserId).ToListAsync();
        if (!memberIds.Contains(userId)) return;

        var groups = memberIds.Where(id => id != userId).Select(UserGroup).ToList();
        if (groups.Count > 0)
            await Clients.Groups(groups).SendAsync("UserTyping", conversationId, userId, isTyping);
    }

    // ---------------------------------------------------------------- helpers
    private static MessageDto ToDto(Message m, bool mine) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        Mine = mine,
        Type = m.Type,
        Content = m.Content,
        FileUrl = m.FileUrl,
        FileName = m.FileName,
        FileSize = m.FileSize,
        CreatedAt = m.CreatedAt,
        Status = m.Status
    };

    // Chỉ nhận file đã upload qua /api/files/upload ("/uploads/<tên>"), trả về kích thước; null nếu không hợp lệ.
    private long? GetUploadSize(string? fileUrl)
    {
        const string prefix = "/uploads/";
        if (string.IsNullOrWhiteSpace(fileUrl) || fileUrl.Length > 300 || !fileUrl.StartsWith(prefix)) return null;

        var name = fileUrl[prefix.Length..];
        if (name.Length == 0 || name != Path.GetFileName(name) || name.Contains("..")) return null;

        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var info = new FileInfo(Path.Combine(root, "uploads", name));
        return info.Exists ? info.Length : null;
    }
}

