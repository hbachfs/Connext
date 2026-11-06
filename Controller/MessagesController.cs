using Connext.Api.Data;
using Connext.Api.Model;
using Connext.Api.Models;
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
    private readonly AppDbContext _context;

    public MessagesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] long? beforeId, [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

        // Kiểm tra quyền (phải là thành viên)
        var member = await _context.ConversationMembers
            .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId);
        if (member == null) return Forbid();

        var query = _context.Messages
            .Where(m => m.ConversationId == conversationId);

        // Soft delete: Không lấy các tin nhắn cũ hơn lúc ClearedAt
        if (member.ClearedAt.HasValue)
            query = query.Where(m => m.CreatedAt > member.ClearedAt.Value);

        // Cursor Pagination
        if (beforeId.HasValue)
            query = query.Where(m => m.Id < beforeId.Value);

        // Lấy danh sách (sắp xếp giảm dần để lấy các tin mới nhất trước mốc beforeId)
        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(pageSize + 1) // Lấy dư 1 để check hasMore
            .ToListAsync();

        var hasMore = messages.Count > pageSize;
        if (hasMore) messages.RemoveAt(messages.Count - 1); // Bỏ đi phần tử dư

        // Đảo ngược lại để trả về theo đúng thứ tự thời gian (cũ -> mới) cho frontend
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
            CreatedAt = m.CreatedAt,
            Status = m.Status
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { hasMore, messages = dtos }));
    }
}