using Connext.Api.Data;
using Connext.Api.Entities;
using Connext.Api.Model;
using Connext.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/friends")]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FriendsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetFriends([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string filter = "all", [FromQuery] string sort = "asc", [FromQuery] string? search = null)
    {
        var userId = GetCurrentUserId();

        var query = _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) && f.Status == "Accepted")
            .Select(f => f.RequesterId == userId ? f.Addressee : f.Requester)
            .AsQueryable();

        // Tìm kiếm
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Phone.Contains(search));

        // Lọc online
        if (filter.ToLower() == "online")
            query = query.Where(u => u.IsOnline);

        // Đếm tổng số để hiển thị badge trên frontend
        var total = await query.CountAsync();
        var totalOnline = await query.CountAsync(u => u.IsOnline);
        var totalRequests = await _context.Friendships.CountAsync(f => f.AddresseeId == userId && f.Status == "Pending");

        // Sắp xếp
        query = sort.ToLower() == "desc" ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName);

        var friends = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new FriendDto
            {
                Id = u.Id,
                FullName = u.FullName,
                AvatarUrl = u.AvatarUrl,
                IsOnline = u.IsOnline,
                LastSeenAt = u.LastSeenAt
            }).ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items = friends, total, totalOnline, totalRequests }));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetFriendRequests()
    {
        var userId = GetCurrentUserId();

        var requests = await _context.Friendships
            .Include(f => f.Requester)
            .Where(f => f.AddresseeId == userId && f.Status == "Pending")
            .Select(f => new FriendRequestDto
            {
                Id = f.Requester.Id,
                FullName = f.Requester.FullName,
                AvatarUrl = f.Requester.AvatarUrl,
                CreatedAt = f.CreatedAt,
                MutualFriendsCount = 0 // Giữ 0 cho nhẹ, có thể viết thêm logic đếm bạn chung nếu cần thiết
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FriendRequestDto>>.Ok(requests));
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestDto req)
    {
        var userId = GetCurrentUserId();
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Phone == req.Phone);

        if (targetUser == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng với số điện thoại này."));
        if (targetUser.Id == userId) return BadRequest(ApiResponse<object>.Fail("Bạn không thể tự kết bạn với chính mình."));

        var existing = await _context.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == targetUser.Id) ||
            (f.RequesterId == targetUser.Id && f.AddresseeId == userId));

        if (existing != null)
        {
            if (existing.Status == "Accepted") return BadRequest(ApiResponse<object>.Fail("Hai người đã là bạn bè."));
            return BadRequest(ApiResponse<object>.Fail("Đã có lời mời kết bạn đang chờ xử lý."));
        }

        var friendship = new Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUser.Id,
            Status = "Pending"
        };
        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Gửi lời mời kết bạn thành công."));
    }

    [HttpPost("{friendId}/accept")]
    public async Task<IActionResult> AcceptRequest(Guid friendId)
    {
        var userId = GetCurrentUserId();
        var request = await _context.Friendships.FirstOrDefaultAsync(f => f.RequesterId == friendId && f.AddresseeId == userId && f.Status == "Pending");

        if (request == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy lời mời kết bạn."));

        request.Status = "Accepted";

        // Tự động tạo hội thoại 1-1 nếu chưa có
        var existingConv = await _context.Conversations
            .Where(c => !c.IsGroup && c.Members.Any(m => m.UserId == userId) && c.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        Guid conversationId;

        if (existingConv == null)
        {
            var conv = new Conversation { IsGroup = false, CreatedById = userId };
            conv.Members.Add(new ConversationMember { UserId = userId, Role = "Member" });
            conv.Members.Add(new ConversationMember { UserId = friendId, Role = "Member" });
            _context.Conversations.Add(conv);
            await _context.SaveChangesAsync();
            conversationId = conv.Id;
        }
        else
        {
            conversationId = existingConv.Id;
            // Nếu đã từng xóa hội thoại (ClearedAt), khôi phục lại
            foreach (var member in existingConv.Members) member.ClearedAt = null;
            await _context.SaveChangesAsync();
        }

        return Ok(ApiResponse<object>.Ok(new { conversationId }, "Đã chấp nhận lời mời."));
    }

    [HttpPost("{friendId}/decline")]
    public async Task<IActionResult> DeclineRequest(Guid friendId)
    {
        var userId = GetCurrentUserId();
        var request = await _context.Friendships.FirstOrDefaultAsync(f => f.RequesterId == friendId && f.AddresseeId == userId && f.Status == "Pending");

        if (request != null)
        {
            _context.Friendships.Remove(request);
            await _context.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(null, "Đã từ chối lời mời."));
    }

    [HttpDelete("{friendId}")]
    public async Task<IActionResult> Unfriend(Guid friendId)
    {
        var userId = GetCurrentUserId();
        var request = await _context.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == friendId && f.AddresseeId == userId) ||
            (f.RequesterId == userId && f.AddresseeId == friendId));

        if (request != null)
        {
            _context.Friendships.Remove(request);
            await _context.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(null, "Đã hủy kết bạn."));
    }
}