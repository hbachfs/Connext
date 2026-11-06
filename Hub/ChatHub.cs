using Connext.Api.Data;
using Connext.Api.Entities;
using Connext.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Connext.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _context;

    // Theo dõi các kết nối của mỗi User (1 User có thể mở nhiều tab/trình duyệt)
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

    public ChatHub(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        var connectionId = Context.ConnectionId;

        // Thêm connection
        var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections) { connections.Add(connectionId); }

        // Nếu đây là connection đầu tiên -> User vừa online
        if (connections.Count == 1)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsOnline = true;
                await _context.SaveChangesAsync();

                // Broadcast cho tất cả (trong thực tế nên chỉ phát cho danh sách bạn bè, nhưng để đơn giản ta phát All)
                await Clients.All.SendAsync("UserOnlineChanged", userId, true, null);
            }
        }

        // Tự động Join vào các Group hội thoại của user này
        var conversations = await _context.ConversationMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.ConversationId)
            .ToListAsync();

        foreach (var convId in conversations)
        {
            await Groups.AddToGroupAsync(connectionId, $"conv-{convId}");
        }

        // Group cá nhân để nhận thông báo (như kết bạn)
        await Groups.AddToGroupAsync(connectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var connectionId = Context.ConnectionId;

        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections) { connections.Remove(connectionId); }

            // Nếu không còn connection nào -> Offline
            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.LastSeenAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await Clients.All.SendAsync("UserOnlineChanged", userId, false, user.LastSeenAt);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // 1. Client gọi để gửi tin nhắn
    public async Task SendMessage(Guid conversationId, string type, string content, string? fileUrl, string? fileName)
    {
        var userId = GetCurrentUserId();

        // Kiểm tra quyền
        var isMember = await _context.ConversationMembers.AnyAsync(cm => cm.UserId == userId && cm.ConversationId == conversationId);
        if (!isMember) return;

        // Lưu DB
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Type = type,
            Content = content,
            FileUrl = fileUrl,
            FileName = fileName,
            Status = "Sent" // Mặc định là đã gửi
        };

        _context.Messages.Add(message);

        // Nếu hội thoại đã bị soft-delete trước đó, khôi phục lại cho mọi người
        var members = await _context.ConversationMembers.Where(cm => cm.ConversationId == conversationId).ToListAsync();
        foreach (var member in members)
        {
            member.ClearedAt = null;
        }

        await _context.SaveChangesAsync();

        var msgDto = new MessageDto
        {
            Id = message.Id,
            ConversationId = conversationId,
            SenderId = userId,
            Mine = false, // Sẽ được phía client tự xác định
            Type = type,
            Content = content,
            FileUrl = fileUrl,
            FileName = fileName,
            CreatedAt = message.CreatedAt,
            Status = "Sent"
        };

        // Gửi tin nhắn cho toàn bộ thành viên trong Group hội thoại
        await Clients.Group($"conv-{conversationId}").SendAsync("ReceiveMessage", msgDto);

        // Báo lại cho người gửi rằng tin nhắn đã chạm server (Delivered)
        await Clients.Caller.SendAsync("MessageDelivered", conversationId, message.Id);
    }

    // 2. Client gọi khi mở hội thoại để đánh dấu Đã xem (bắt buộc có)
    public async Task MarkAsRead(Guid conversationId)
    {
        var userId = GetCurrentUserId();

        var member = await _context.ConversationMembers.FirstOrDefaultAsync(cm => cm.UserId == userId && cm.ConversationId == conversationId);
        if (member == null) return;

        // Tìm tin nhắn mới nhất trong nhóm
        var lastMsg = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        if (lastMsg != null && lastMsg.Id > member.LastReadMessageId)
        {
            member.LastReadMessageId = lastMsg.Id;

            // Cập nhật trạng thái tin nhắn (để đơn giản, ta chỉ báo Seen mà không cần update từng dòng Message.Status trong DB vì sẽ gây chậm)
            await _context.SaveChangesAsync();

            // Phát sự kiện "Đã xem" cho những người khác trong nhóm
            await Clients.Group($"conv-{conversationId}").SendAsync("MessagesSeen", conversationId, userId, lastMsg.Id);
        }
    }
}