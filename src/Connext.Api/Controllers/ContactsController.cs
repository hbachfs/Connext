using System.Security.Claims;
using Connext.Api.Services;
using Connext.Shared.DTOs.Contacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, Required, MinLength(2)] string query)
    {
        var results = await _contactService.SearchUsersAsync(CurrentUserId, query);
        return Ok(results);
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] FriendshipRequestDto dto)
    {
        var result = await _contactService.SendFriendRequestAsync(CurrentUserId, dto.PhoneNumber);
        return result ? Ok() : BadRequest("Không thể gửi lời mời kết bạn.");
    }

    [HttpGet("friends")]
    public async Task<IActionResult> GetFriends()
    {
        var friends = await _contactService.GetFriendsAsync(CurrentUserId);
        return Ok(friends);
    }

    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] FriendshipRespondDto dto)
    {
        var result = await _contactService.RespondToRequestAsync(CurrentUserId, dto.RequestId, dto.Accept);
        return result ? Ok() : BadRequest("Thao tác không hợp lệ.");
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests()
    {
        return Ok(await _contactService.GetRequestsAsync(CurrentUserId));
    }

    [HttpDelete("friends/{friendId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid friendId)
    {
        var result = await _contactService.RemoveFriendAsync(CurrentUserId, friendId);
        return result ? NoContent() : NotFound("Không tìm thấy quan hệ bạn bè.");
    }

    [HttpPost("blocks/{userId:guid}")]
    public async Task<IActionResult> BlockUser(Guid userId)
    {
        var result = await _contactService.BlockUserAsync(CurrentUserId, userId);
        return result ? NoContent() : BadRequest("Không thể chặn người dùng này.");
    }

    [HttpDelete("blocks/{userId:guid}")]
    public async Task<IActionResult> UnblockUser(Guid userId)
    {
        var result = await _contactService.UnblockUserAsync(CurrentUserId, userId);
        return result ? NoContent() : NotFound("Không tìm thấy lượt chặn.");
    }
}
