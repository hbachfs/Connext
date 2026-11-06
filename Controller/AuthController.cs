using Connext.Api.Data;
using Connext.Api.Entities;
using Connext.Api.Model;
using Connext.Api.Models;
using Connext.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly EmailService _emailService;

    public AuthController(AppDbContext context, JwtHelper jwtHelper, EmailService emailService)
    {
        _context = context;
        _jwtHelper = jwtHelper;
        _emailService = emailService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (await _context.Users.AnyAsync(u => u.Phone == req.Phone))
            return BadRequest(ApiResponse<object>.Fail("Số điện thoại đã được sử dụng."));

        if (await _context.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(ApiResponse<object>.Fail("Email đã được sử dụng."));

        var user = new User
        {
            FullName = req.FullName,
            Phone = req.Phone,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đăng ký thành công."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == req.Phone);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return BadRequest(ApiResponse<object>.Fail("Số điện thoại hoặc mật khẩu không đúng."));

        var token = _jwtHelper.GenerateToken(user);

        return Ok(ApiResponse<object>.Ok(new
        {
            token,
            user = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Gender = user.Gender
            }
        }, "Đăng nhập thành công."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null)
            return BadRequest(ApiResponse<object>.Fail("Không tìm thấy tài khoản với email này."));

        // Tạo OTP 6 số
        var otp = new Random().Next(100000, 999999).ToString();

        var otpRecord = new PasswordResetOtp
        {
            Email = req.Email,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.PasswordResetOtps.Add(otpRecord);
        await _context.SaveChangesAsync();

        // Gửi email
        try
        {
            await _emailService.SendEmailAsync(req.Email, "Mã xác thực quên mật khẩu - Connext",
                $"Mã OTP của bạn là: <b>{otp}</b>. Mã này sẽ hết hạn sau 5 phút.");
        }
        catch
        {
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi gửi email. Vui lòng kiểm tra cấu hình SMTP."));
        }

        return Ok(ApiResponse<object>.Ok(null, "Mã OTP đã được gửi đến email của bạn."));
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest req)
    {
        var otpRecord = await _context.PasswordResetOtps
            .Where(o => o.Email == req.Email && !o.IsUsed)
            .OrderByDescending(o => o.ExpiresAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null || otpRecord.ExpiresAt < DateTime.UtcNow)
            return BadRequest(ApiResponse<object>.Fail("Mã OTP không hợp lệ hoặc đã hết hạn."));

        if (otpRecord.FailedAttempts >= 5)
            return BadRequest(ApiResponse<object>.Fail("Bạn đã nhập sai quá nhiều lần. Vui lòng gửi lại mã mới."));

        if (!BCrypt.Net.BCrypt.Verify(req.Otp, otpRecord.CodeHash))
        {
            otpRecord.FailedAttempts++;
            await _context.SaveChangesAsync();
            return BadRequest(ApiResponse<object>.Fail("Mã OTP không chính xác."));
        }

        // OTP đúng -> đánh dấu đã dùng và cấp 1 token tạm để gọi API đổi mật khẩu
        otpRecord.IsUsed = true;
        await _context.SaveChangesAsync();

        var resetToken = _jwtHelper.GenerateResetToken(req.Email);
        return Ok(ApiResponse<object>.Ok(new { resetToken }, "Xác thực thành công."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var jwtToken = tokenHandler.ReadJwtToken(req.ResetToken);
            if (jwtToken.ValidTo < DateTime.UtcNow || jwtToken.Claims.FirstOrDefault(c => c.Type == "Purpose")?.Value != "ResetPassword")
                throw new Exception();

            var email = jwtToken.Claims.First(c => c.Type == ClaimTypes.Email).Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null) return BadRequest(ApiResponse<object>.Fail("Tài khoản không tồn tại."));

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(null, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."));
        }
        catch
        {
            return BadRequest(ApiResponse<object>.Fail("Token đổi mật khẩu không hợp lệ hoặc đã hết hạn."));
        }
    }
}
