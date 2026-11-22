using System.Security.Claims;
using Connext.Api.Services;
using Connext.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public UsersController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var profile = await _profileService.GetAsync(CurrentUserId);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequestDto request)
    {
        try
        {
            var profile = await _profileService.UpdateAsync(CurrentUserId, request);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var changed = await _profileService.ChangePasswordAsync(CurrentUserId, request);
        return changed
            ? NoContent()
            : BadRequest(new { message = "Mật khẩu hiện tại không chính xác." });
    }
}
