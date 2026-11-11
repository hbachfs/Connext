using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations/{conversationId}/messages")]
public class MessagesController : ControllerBase
{
    private const int MaxPageSize = 50;

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
            .OrderByDescending(m => m.Id)
            .Take(pageSize + 1) // Lấy dư 1 để check hasMore
            .ToListAsync();

        var hasMore = messages.Count > pageSize;
        if (hasMore) messages.RemoveAt(messages.Count - 1);

        // Đảo lại thành cũ -> mới cho frontend
        messages.Reverse();

        var dtos = messages.Select(m => new MessageDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            Mine = m.SenderId == userId,
            Type = m.Type,
            Content = m.Content,
            FileUrl = m.FileUrl,
            FileName = m.FileName,
            FileSize = m.FileSize,
            CreatedAt = m.CreatedAt,
            Status = m.Status
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { hasMore, messages = dtos }));
    }
}

