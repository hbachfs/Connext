using Connext.Api.Services;
using Connext.Shared.DTOs.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Connext.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly LoginAttemptTracker _loginAttemptTracker;

    public AuthController(IAuthService authService, LoginAttemptTracker loginAttemptTracker)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _loginAttemptTracker = loginAttemptTracker ?? throw new ArgumentNullException(nameof(loginAttemptTracker));
    }

    /// <summary>
    /// Đăng ký tài khoản mới.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Đăng nhập bằng số điện thoại và mật khẩu.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (_loginAttemptTracker.IsLimited(request.PhoneNumber, DateTime.UtcNow))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Quá nhiều lần đăng nhập không thành công. Vui lòng thử lại sau." });

        var result = await _authService.LoginAsync(request);

        if (result is null)
        {
            _loginAttemptTracker.RecordFailure(request.PhoneNumber, DateTime.UtcNow);
            return Unauthorized(new { message = "Số điện thoại hoặc mật khẩu không chính xác." });
        }

        _loginAttemptTracker.Reset(request.PhoneNumber);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        await _authService.LogoutAsync(userId);
        return NoContent();
    }
}
