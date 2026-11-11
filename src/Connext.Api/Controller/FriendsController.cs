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
[Route("api/friends")]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hub;

    public FriendsController(AppDbContext context, IHubContext<ChatHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetFriends([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string filter = "all", [FromQuery] string sort = "asc", [FromQuery] string? search = null)
    {
        var userId = GetCurrentUserId();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Lấy id bạn bè bằng subquery rồi truy vấn User (tránh chiếu entity qua toán tử điều kiện, EF khó dịch sang SQL)
        var friendIds = _context.Friendships
            .Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId);

        var query = _context.Users.AsNoTracking().Where(u => friendIds.Contains(u.Id));

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Phone.Contains(search));

        // Đếm tổng số để hiển thị badge trên frontend (trước khi lọc online)
        var total = await query.CountAsync();
        var totalOnline = await query.CountAsync(u => u.IsOnline);
        var totalRequests = await _context.Friendships.CountAsync(f => f.AddresseeId == userId && f.Status == "Pending");

        if (filter.Equals("online", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.IsOnline);

        query = sort.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(u => u.FullName)
            : query.OrderBy(u => u.FullName);

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

        var requests = await _context.Friendships.AsNoTracking()
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

        _context.Friendships.Add(new Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUser.Id,
            Status = "Pending"
        });
        await _context.SaveChangesAsync();

        // Báo realtime cho người nhận lời mời
        await _hub.Clients.Group(ChatHub.UserGroup(targetUser.Id)).SendAsync("FriendRequestReceived", userId);

        return Ok(ApiResponse<object>.Ok(null, "Gửi lời mời kết bạn thành công."));
    }

    [HttpPost("{friendId}/accept")]
    public async Task<IActionResult> AcceptRequest(Guid friendId)
    {
        var userId = GetCurrentUserId();

        // 1 câu lệnh nguyên tử: chỉ chuyển Pending -> Accepted một lần
        var updated = await _context.Friendships
            .Where(f => f.RequesterId == friendId && f.AddresseeId == userId && f.Status == "Pending")
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, "Accepted"));
        if (updated == 0) return NotFound(ApiResponse<object>.Fail("Không tìm thấy lời mời kết bạn."));

        // Tìm/tạo hội thoại 1-1 (mỗi cặp chỉ 1 hội thoại, hiện lại nếu từng ẩn)
        var conversationId = await DirectConversations.GetOrCreateAsync(_context, userId, friendId);

        // Người gửi lời mời biết ngay là đã được chấp nhận
        await _hub.Clients.Group(ChatHub.UserGroup(friendId)).SendAsync("FriendRequestAccepted", userId, conversationId);

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

    // Hủy kết bạn: hội thoại và lịch sử giữ nguyên, nhưng ChatHub từ chối gửi tin mới giữa hai người
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
            await _hub.Clients.Group(ChatHub.UserGroup(friendId)).SendAsync("Unfriended", userId);
        }
        return Ok(ApiResponse<object>.Ok(null, "Đã hủy kết bạn."));
    }
}
