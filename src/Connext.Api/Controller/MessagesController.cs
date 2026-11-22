using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Api.Services;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations/{conversationId}/messages")]
public class MessagesController : ControllerBase
{
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 10;
    private readonly AppDbContext _context;

    public MessagesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] long? beforeId, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Kiểm tra quyền (phải là thành viên)
        var member = await _context.ConversationMembers.AsNoTracking()
            .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId);
        if (member == null) return Forbid();

        var query = _context.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        // Soft delete: không lấy các tin cũ hơn mốc ClearedAt
        if (member.ClearedAt.HasValue)
        {
            var clearedAt = member.ClearedAt.Value;
            query = query.Where(m => m.CreatedAt > clearedAt);
        }

        // Cursor Pagination
        if (beforeId.HasValue)
            query = query.Where(m => m.Id < beforeId.Value);

        var messages = await query
            .Include(m => m.Mentions)          // lịch sử trả đủ người được @mention
            .Include(m => m.Reactions)         // reaction
            .Include(m => m.ReplyToMessage)    // nội dung tin gốc khi trả lời
            .AsSplitQuery()                    // tránh nổ số dòng khi Include nhiều collection
            .OrderByDescending(m => m.Id)
            .Take(pageSize + 1) // Lấy dư 1 để check hasMore
            .ToListAsync();

        var hasMore = messages.Count > pageSize;
        if (hasMore) messages.RemoveAt(messages.Count - 1);

        // Đảo lại thành cũ -> mới cho frontend
        messages.Reverse();

        // SỬA: dùng MessageMapper chung (đã xử lý thu hồi, reply, reaction, mention)
        var dtos = messages.Select(m =>
        {
            var dto = MessageMapper.ToDto(m, m.SenderId == userId);

            // THÊM: tin gốc nằm trước mốc xóa lịch sử thì không lộ nội dung
            if (member.ClearedAt.HasValue && dto.ReplyTo != null &&
                m.ReplyToMessage!.CreatedAt <= member.ClearedAt.Value)
                dto.ReplyTo.Preview = null;

            return dto;
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { hasMore, messages = dtos }));
    }
}