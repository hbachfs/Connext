using Connext.Api.Data;
using Connext.Api.Dto;
using Connext.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConversationsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Danh sách hội thoại: tin cuối, số tin chưa đọc, ghim, trạng thái online của đối phương
    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var userId = GetCurrentUserId();

        var memberships = await _context.ConversationMembers.AsNoTracking()
            .Where(cm => cm.UserId == userId && !cm.IsHidden)
            .Select(cm => new
            {
                cm.ConversationId,
                cm.IsPinned,
                cm.ClearedAt,
                cm.Conversation.IsGroup,
                cm.Conversation.Name,
                cm.Conversation.AvatarUrl,
                cm.Conversation.CreatedAt,
                MemberCount = cm.Conversation.Members.Count
            })
            .ToListAsync();

        var convIds = memberships.Select(m => m.ConversationId).ToList();

        var others = await _context.ConversationMembers.AsNoTracking()
            .Where(cm => convIds.Contains(cm.ConversationId) && cm.UserId != userId)
            .Select(cm => new { cm.ConversationId, cm.User.FullName, cm.User.AvatarUrl, cm.User.IsOnline, cm.User.LastSeenAt })
            .ToListAsync();
        var otherByConv = others.GroupBy(o => o.ConversationId).ToDictionary(g => g.Key, g => g.First());

        var lastIds = await _context.Messages.AsNoTracking()
            .Where(m => convIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.Max(m => m.Id))
            .ToListAsync();
        var lastMsgs = await _context.Messages.AsNoTracking()
            .Where(m => lastIds.Contains(m.Id))
            .Select(m => new { m.ConversationId, m.Type, m.Content, m.CreatedAt })
            .ToListAsync();
        var lastByConv = lastMsgs.ToDictionary(m => m.ConversationId);

        // Tin chưa đọc = tin của người khác, mới hơn LastReadMessageId và mới hơn mốc ClearedAt
        var unreadRows = await _context.Messages.AsNoTracking()
            .Where(m => m.SenderId != userId && _context.ConversationMembers.Any(cm =>
                cm.UserId == userId && cm.ConversationId == m.ConversationId &&
                m.Id > cm.LastReadMessageId && (cm.ClearedAt == null || m.CreatedAt > cm.ClearedAt)))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync();
        var unreadByConv = unreadRows.ToDictionary(x => x.ConversationId, x => x.Count);

        var result = new List<(ConversationDto Dto, DateTime SortTime)>();
        foreach (var m in memberships)
        {
            var dto = new ConversationDto
            {
                ConversationId = m.ConversationId,
                IsGroup = m.IsGroup,
                IsPinned = m.IsPinned,
                UnreadCount = unreadByConv.GetValueOrDefault(m.ConversationId)
            };

            if (m.IsGroup)
            {
                dto.Name = m.Name ?? "Nhóm";
                dto.AvatarUrl = m.AvatarUrl;
                dto.StatusText = $"{m.MemberCount} thành viên";
            }
            else if (otherByConv.TryGetValue(m.ConversationId, out var other))
            {
                dto.Name = other.FullName;
                dto.AvatarUrl = other.AvatarUrl;
                dto.IsOnline = other.IsOnline;
                dto.StatusText = StatusText(other.IsOnline, other.LastSeenAt);
            }

            var sortTime = m.CreatedAt;
            if (lastByConv.TryGetValue(m.ConversationId, out var last) &&
                !(m.ClearedAt.HasValue && last.CreatedAt <= m.ClearedAt.Value)) // đã xóa hết lịch sử => coi như chưa có tin
            {
                dto.LastMessageType = last.Type;
                dto.LastMessageTime = last.CreatedAt;
                dto.LastMessageContent = last.Type switch
                {
                    "image" => "[Hình ảnh]",
                    "file" => "[Tệp đính kèm]",
                    _ => last.Content
                };
                sortTime = last.CreatedAt;
            }

            result.Add((dto, sortTime));
        }

        var list = result
            .OrderByDescending(x => x.Dto.IsPinned)
            .ThenByDescending(x => x.SortTime)
            .Select(x => x.Dto)
            .ToList();

        return Ok(ApiResponse<List<ConversationDto>>.Ok(list));
    }

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