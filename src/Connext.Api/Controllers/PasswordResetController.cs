using Connext.Api.Services;
using Connext.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Connext.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class PasswordResetController : ControllerBase
{
    private readonly IPasswordResetService _passwordResetService;

    public PasswordResetController(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        await _passwordResetService.RequestOtpAsync(request.Email);
        return Accepted(new { message = "Nếu email đã đăng ký, mã xác minh sẽ được gửi đến hộp thư." });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request)
    {
        var token = await _passwordResetService.VerifyOtpAsync(request.Email, request.Code);
        return token is null
            ? BadRequest(new { message = "Mã xác minh không hợp lệ hoặc đã hết hạn." })
            : Ok(new { token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var result = await _passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword);
        return result
            ? NoContent()
            : BadRequest(new { message = "Token đặt lại mật khẩu không hợp lệ hoặc đã được sử dụng." });
    }
}
