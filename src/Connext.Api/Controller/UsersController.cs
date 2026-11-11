using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await _context.Users.FindAsync(GetCurrentUserId());
        if (user == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng."));

        return Ok(ApiResponse<UserProfileDto>.Ok(new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Gender = user.Gender,
            AvatarUrl = user.AvatarUrl
        }));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req)
    {
        var userId = GetCurrentUserId();

        // Kiểm tra trùng lặp email/phone nhưng bỏ qua chính mình
        if (await _context.Users.AnyAsync(u => u.Phone == req.Phone && u.Id != userId))
            return BadRequest(ApiResponse<object>.Fail("Số điện thoại đã được người khác sử dụng."));

        if (await _context.Users.AnyAsync(u => u.Email == req.Email && u.Id != userId))
            return BadRequest(ApiResponse<object>.Fail("Email đã được người khác sử dụng."));

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng."));

        user.FullName = req.FullName;
        user.Phone = req.Phone;
        user.Email = req.Email;
        user.Gender = req.Gender;

        // Chỉ cập nhật avatar nếu có gửi lên
        if (req.AvatarUrl != null)
            user.AvatarUrl = req.AvatarUrl;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Gender = user.Gender,
            AvatarUrl = user.AvatarUrl
        }, "Cập nhật hồ sơ thành công."));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await _context.Users.FindAsync(GetCurrentUserId());
        if (user == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng."));

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu hiện tại không đúng."));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đổi mật khẩu thành công."));
    }
}

