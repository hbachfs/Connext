using Connext.Api.Hubs;
using Connext.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private const int MinimumGroupMembers = 3;
    private const int MaximumGroupMembers = 100;

    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub>? _hub;

    public ConversationsController(AppDbContext context, IHubContext<ChatHub>? hub = null)
    {
        _context = context;
        _hub = hub;
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Token không chứa user id hợp lệ.");
    }

    // Danh sách hội thoại: tin cuối, số tin chưa đọc, ghim, trạng thái online của đối phương
    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var userId = GetCurrentUserId();

        var items = await _context.ConversationMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId && !cm.IsHidden)
            .Select(cm => new
            {
                cm.ConversationId,
                cm.IsPinned,
                cm.Conversation.IsGroup,
                cm.Conversation.Name,
                cm.Conversation.AvatarUrl,
                cm.Conversation.CreatedAt,
                MemberCount = cm.Conversation.Members.Count(),
                OtherName = cm.Conversation.Members.Where(m => m.UserId != userId)
                    .Select(m => m.User.FullName).FirstOrDefault(),
                OtherAvatar = cm.Conversation.Members.Where(m => m.UserId != userId)
                    .Select(m => m.User.AvatarUrl).FirstOrDefault(),
                OtherOnline = cm.Conversation.Members.Where(m => m.UserId != userId)
                    .Select(m => m.User.IsOnline).FirstOrDefault(),
                OtherLastSeen = cm.Conversation.Members.Where(m => m.UserId != userId)
                    .Select(m => m.User.LastSeenAt).FirstOrDefault(),
                // Chỉ lấy tin sau mốc xóa hội thoại (ClearedAt)
                LastMessage = cm.Conversation.Messages
                    .Where(m => !cm.ClearedAt.HasValue || m.CreatedAt > cm.ClearedAt.Value)
                    .OrderByDescending(m => m.Id)
                    .Select(m => new { m.Content, m.CreatedAt, m.Type })
                    .FirstOrDefault(),
                UnreadCount = cm.Conversation.Messages.Count(m =>
                    m.SenderId != userId &&
                    m.Id > cm.LastReadMessageId &&
                    (!cm.ClearedAt.HasValue || m.CreatedAt > cm.ClearedAt.Value))
            })
            .ToListAsync();

        var result = items
            .OrderByDescending(x => x.IsPinned) // ghim lên đầu
            .ThenByDescending(x => x.LastMessage?.CreatedAt ?? x.CreatedAt)
            .Select(x => new ConversationDto
            {
                ConversationId = x.ConversationId,
                IsGroup = x.IsGroup,
                Name = x.IsGroup ? (x.Name ?? "Nhóm không tên") : (x.OtherName ?? "Hội thoại"),
                AvatarUrl = x.IsGroup ? x.AvatarUrl : x.OtherAvatar,
                IsOnline = !x.IsGroup && x.OtherOnline,
                StatusText = x.IsGroup ? $"{x.MemberCount} thành viên" : StatusText(x.OtherOnline, x.OtherLastSeen),
                LastMessageContent = x.LastMessage == null ? null : Preview(x.LastMessage.Type, x.LastMessage.Content),
                LastMessageTime = x.LastMessage?.CreatedAt,
                LastMessageType = x.LastMessage?.Type,
                UnreadCount = x.UnreadCount,
                IsPinned = x.IsPinned
            })
            .ToList();

        return Ok(ApiResponse<List<ConversationDto>>.Ok(result));
    }

    private static string? Preview(string type, string? content) => type switch
    {
        "image" => "[Hình ảnh]",
        "file" => "[Tệp đính kèm]",
        _=> content
    };

    // Mở (hoặc tạo) hội thoại 1-1 với một người bạn
    [HttpPost("direct/{friendId:guid}")]
    public async Task<IActionResult> OpenDirect(Guid friendId)
    {
        var userId = GetCurrentUserId();
        if (friendId == userId) return BadRequest(ApiResponse<object>.Fail("Không thể nhắn tin cho chính mình."));

        var areFriends = await _context.Friendships.AsNoTracking().AnyAsync(f => f.Status == "Accepted" &&
            ((f.RequesterId == userId && f.AddresseeId == friendId) || (f.RequesterId == friendId && f.AddresseeId == userId)));
        if (!areFriends) return BadRequest(ApiResponse<object>.Fail("Hai bạn chưa là bạn bè."));

        var conversationId = await DirectConversations.GetOrCreateAsync(_context, userId, friendId);
        return Ok(ApiResponse<object>.Ok(new { conversationId }));
    }

    [HttpPut("{id:guid}/pin")]
    public async Task<IActionResult> Pin(Guid id, PinConversationRequest req)
    {
        var userId = GetCurrentUserId();
        var updated = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == id && cm.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(cm => cm.IsPinned, req.IsPinned));

        return updated == 0
            ? NotFound(ApiResponse<object>.Fail("Không tìm thấy hội thoại."))
            : Ok(ApiResponse<object>.Ok(null, req.IsPinned ? "Đã ghim hội thoại." : "Đã bỏ ghim."));
    }

    // Xóa hội thoại (chỉ phía mình): cắt lịch sử tại thời điểm này và ẩn khỏi danh sách
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        DateTime? now = DateTime.UtcNow;
        var updated = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == id && cm.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(cm => cm.ClearedAt, now)
                .SetProperty(cm => cm.IsHidden, true)
                .SetProperty(cm => cm.IsPinned, false));

        return updated == 0
            ? NotFound(ApiResponse<object>.Fail("Không tìm thấy hội thoại."))
            : Ok(ApiResponse<object>.Ok(null, "Đã xóa hội thoại."));
    }

    // Tạo một nhóm mới
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        var userId = GetCurrentUserId();
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm không được để trống."));

        if (name.Length > 100)
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm không được vượt quá 100 ký tự."));

        var memberIds = (request.MemberIds ?? new List<Guid>())
            .Append(userId)
            .Distinct()
            .ToList();

        if (memberIds.Count < MinimumGroupMembers)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Nhóm phải có ít nhất 3 thành viên, bao gồm cả người tạo nhóm."));
        }

        if (memberIds.Count > MaximumGroupMembers)
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Nhóm không được vượt quá {MaximumGroupMembers} thành viên."));
        }

        var users = await _context.Users
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var missingUserIds = memberIds.Except(users).ToList();
        if (missingUserIds.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Một hoặc nhiều thành viên không tồn tại."));

        var friendIds = await _context.Friendships
            .Where(f => f.Status == "Accepted" &&
                (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        var nonFriends = memberIds
            .Where(id => id != userId && !friendIds.Contains(id))
            .ToList();

        if (nonFriends.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Bạn chỉ có thể thêm những người đã là bạn bè."));
        }

        var conversation = new Conversation
        {
            IsGroup = true,
            Name = name,
            CreatedById = userId
        };

        foreach (var memberId in memberIds)
        {
            conversation.Members.Add(new ConversationMember
            {
                UserId = memberId,
                Role = memberId == userId ? "Admin" : "Member"
            });
        }

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
        await NotifyAsync(memberIds.Where(id => id != userId), "AddedToConversation", conversation.Id);

        return Ok(ApiResponse<object>.Ok(
            new
            {
                conversationId = conversation.Id,
                name = conversation.Name,
                memberCount = memberIds.Count
            },
            "Tạo nhóm thành công."));
    }
    // Danh sách thành viên (mọi thành viên đều xem được)
    [HttpGet("groups/{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var userId = GetCurrentUserId();
        var isMember = await _context.ConversationMembers
            .AnyAsync(cm => cm.ConversationId == id && cm.UserId == userId);
        if (!isMember) return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));

        var members = await _context.ConversationMembers.AsNoTracking()
            .Where(cm => cm.ConversationId == id)
            .OrderBy(cm => cm.JoinedAt)
            .Select(cm => new
            {
                id = cm.UserId,
                fullName = cm.User.FullName,
                avatarUrl = cm.User.AvatarUrl,
                role = cm.Role,
                isOnline = cm.User.IsOnline
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(members));
    }

    // Thêm thành viên (chỉ quản trị viên, chỉ thêm bạn bè của mình)
    [HttpPost("groups/{id:guid}/members")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddMembersRequest request)
    {
        var userId = GetCurrentUserId();
        var group = await LoadGroupAsync(id);
        var me = group?.Members.FirstOrDefault(m => m.UserId == userId);
        if (group == null || me == null)
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        if (me.Role != "Admin")
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Chỉ quản trị viên nhóm mới được thêm thành viên."));

        var newIds = (request.MemberIds ?? new List<Guid>())
            .Distinct()
            .Where(x => group.Members.All(m => m.UserId != x))
            .ToList();
        if (newIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Không có thành viên mới để thêm."));

        if (group.Members.Count + newIds.Count > MaximumGroupMembers)
            return BadRequest(ApiResponse<object>.Fail($"Nhóm không được vượt quá {MaximumGroupMembers} thành viên."));

        var friendIds = await GetFriendIdsAsync(userId);
        if (newIds.Any(x => !friendIds.Contains(x)))
            return BadRequest(ApiResponse<object>.Fail("Bạn chỉ có thể thêm những người đã là bạn bè."));

        // Người mới không thấy tin cũ: đặt mốc cắt lịch sử và coi như đã đọc đến tin hiện tại
        var now = DateTime.UtcNow;
        var lastMsgId = await _context.Messages
            .Where(m => m.ConversationId == id)
            .MaxAsync(m => (long?)m.Id) ?? 0;

        foreach (var newId in newIds)
        {
            group.Members.Add(new ConversationMember
            {
                UserId = newId,
                Role = "Member",
                ClearedAt = now,
                LastReadMessageId = lastMsgId
            });
        }
        await _context.SaveChangesAsync();

        await NotifyAsync(newIds, "AddedToConversation", id);
        await NotifyAsync(group.Members.Select(m => m.UserId).Except(newIds), "ConversationUpdated", id);

        return Ok(ApiResponse<object>.Ok(new { memberCount = group.Members.Count }, "Đã thêm thành viên."));
    }

    // Quản trị viên xóa một thành viên khỏi nhóm
    [HttpDelete("groups/{id:guid}/members/{memberId:guid}")]
    public Task<IActionResult> RemoveMember(Guid id, Guid memberId) => RemoveMemberCore(id, memberId);

    // Tự rời nhóm
    [HttpPost("groups/{id:guid}/leave")]
    public Task<IActionResult> Leave(Guid id) => RemoveMemberCore(id, GetCurrentUserId());

    // Đổi tên nhóm (chỉ quản trị viên)
    [HttpPut("groups/{id:guid}/name")]
    public async Task<IActionResult> RenameGroup(Guid id, [FromBody] RenameGroupRequest request)
    {
        var userId = GetCurrentUserId();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm không được để trống."));
        if (name.Length > 100)
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm không được vượt quá 100 ký tự."));

        var group = await LoadGroupAsync(id);
        var me = group?.Members.FirstOrDefault(m => m.UserId == userId);
        if (group == null || me == null)
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        if (me.Role != "Admin")
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Chỉ quản trị viên nhóm mới được đổi tên."));

        group.Name = name;
        await _context.SaveChangesAsync();

        await NotifyAsync(group.Members.Select(m => m.UserId), "ConversationUpdated", id);
        return Ok(ApiResponse<object>.Ok(new { name }, "Đã đổi tên nhóm."));
    }

    // ---------------------------------------------------------------- helpers cho nhóm
    private async Task<IActionResult> RemoveMemberCore(Guid id, Guid targetId)
    {
        var userId = GetCurrentUserId();
        var group = await LoadGroupAsync(id);
        var me = group?.Members.FirstOrDefault(m => m.UserId == userId);
        if (group == null || me == null)
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));

        var target = group.Members.FirstOrDefault(m => m.UserId == targetId);
        if (target == null)
            return NotFound(ApiResponse<object>.Fail("Người này không ở trong nhóm."));

        if (targetId != userId)
        {
            if (me.Role != "Admin")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail("Chỉ quản trị viên nhóm mới được xóa thành viên."));
            if (target.Role == "Admin")
                return BadRequest(ApiResponse<object>.Fail("Không thể xóa quản trị viên khác khỏi nhóm."));
        }

        // Quy tắc 3 người: không để nhóm tụt xuống dưới mức tối thiểu
        if (group.Members.Count - 1 < MinimumGroupMembers)
            return BadRequest(ApiResponse<object>.Fail(
                $"Nhóm phải còn ít nhất {MinimumGroupMembers} thành viên. Hãy xóa hội thoại thay vì rời nhóm."));

        _context.ConversationMembers.Remove(target);

        // Quản trị viên cuối cùng rời đi: chuyển quyền cho người vào nhóm sớm nhất
        if (target.Role == "Admin" && !group.Members.Any(m => m.UserId != targetId && m.Role == "Admin"))
        {
            group.Members.Where(m => m.UserId != targetId).OrderBy(m => m.JoinedAt).First().Role = "Admin";
        }

        await _context.SaveChangesAsync();

        var remaining = group.Members.Where(m => m.UserId != targetId).Select(m => m.UserId).ToList();
        await NotifyAsync(new[] { targetId }, "RemovedFromConversation", id);
        await NotifyAsync(remaining, "ConversationUpdated", id);

        return Ok(ApiResponse<object>.Ok(null, targetId == userId ? "Bạn đã rời nhóm." : "Đã xóa thành viên khỏi nhóm."));
    }

    private Task<Conversation?> LoadGroupAsync(Guid id) =>
        _context.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id && c.IsGroup);

    private Task<List<Guid>> GetFriendIdsAsync(Guid userId) =>
        _context.Friendships.AsNoTracking()
            .Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

    private Task NotifyAsync(IEnumerable<Guid> userIds, string method, params object[] args)
    {
        if (_hub == null) return Task.CompletedTask;
        var groups = userIds.Select(ChatHub.UserGroup).ToList();
        return groups.Count == 0
            ? Task.CompletedTask
            : _hub.Clients.Groups(groups).SendCoreAsync(method, args);
    }

    private static string StatusText(bool isOnline, DateTime? lastSeen)
    {
        if (isOnline) return "Đang hoạt động";
        if (lastSeen == null) return "Ngoại tuyến";

        var ago = DateTime.UtcNow - lastSeen.Value;
        if (ago.TotalMinutes < 1) return "Vừa truy cập";
        if (ago.TotalMinutes < 60) return $"Hoạt động {(int)ago.TotalMinutes} phút trước";
        if (ago.TotalHours < 24) return $"Hoạt động {(int)ago.TotalHours} giờ trước";
        return $"Hoạt động {(int)ago.TotalDays} ngày trước";
    }
}
