using Connext.Api.Hubs;
using Connext.Api.Services;
using Connext.Shared;
using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations/{conversationId:guid}/polls")]
public class PollsController(IPollService polls, IHubContext<ChatHub> hub) : ControllerBase
{
    private Guid GetCurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new UnauthorizedAccessException("Token không chứa user id hợp lệ.");

    [HttpPost]
    public async Task<IActionResult> Create(Guid conversationId, [FromBody] CreatePollRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await polls.CreateAsync(userId, conversationId, request.Question, request.Options, ct);
        if (!r.Ok) return Fail(r.Error);

        var poll = r.Poll!;
        foreach (var uid in r.MemberIds)
        {
            var dto = MessageMapper.ToDto(r.Message!, uid == userId);
            dto.Poll = poll;
            await hub.Clients.Group(ChatHub.UserGroup(uid)).SendAsync("ReceiveMessage", dto, ct);
        }
        await hub.Clients.Groups(r.MemberIds.Select(ChatHub.UserGroup).ToList()).SendAsync("PollCreated", poll, ct);

        return Created($"/api/conversations/{conversationId}/polls/{poll.PollId}",
            ApiResponse<PollDto>.Ok(poll, "Đã tạo bình chọn."));
    }

    [HttpPost("{pollId:guid}/vote")]
    public async Task<IActionResult> Vote(Guid conversationId, Guid pollId, [FromBody] VotePollRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await polls.VoteAsync(userId, conversationId, pollId, request.OptionId, ct);
        if (!r.Ok) return Fail(r.Error);

        var shared = r.Poll! with { MyOptionId = null };   // payload chung, client tự biết phiếu của mình qua VoterId
        await hub.Clients.Groups(r.MemberIds.Select(ChatHub.UserGroup).ToList())
            .SendAsync("PollVoted", new PollVotedDto(shared, userId, request.OptionId), ct);

        return Ok(ApiResponse<PollDto>.Ok(r.Poll!, "Đã ghi nhận bình chọn."));
    }

    [HttpGet("{pollId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, Guid pollId, CancellationToken ct)
    {
        var r = await polls.GetAsync(GetCurrentUserId(), conversationId, pollId, ct);
        return r.Ok ? Ok(ApiResponse<PollDto>.Ok(r.Poll!)) : Fail(r.Error);
    }

    private IActionResult Fail(PollError e) => e switch
    {
        PollError.NotMember => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Bạn không thuộc nhóm này.")),
        PollError.NotFound => NotFound(ApiResponse<object>.Fail("Không tìm thấy bình chọn.")),
        PollError.NotGroup => BadRequest(ApiResponse<object>.Fail("Chỉ tạo được bình chọn trong nhóm.")),
        PollError.InvalidQuestion => BadRequest(ApiResponse<object>.Fail($"Câu hỏi không được để trống và tối đa {Limits.PollQuestionMaxLength} ký tự.")),
        PollError.InvalidOptions => BadRequest(ApiResponse<object>.Fail(
            $"Cần từ {Limits.PollMinOptions} đến {Limits.PollMaxOptions} lựa chọn, không trùng nhau, mỗi lựa chọn tối đa {Limits.PollOptionMaxLength} ký tự.")),
        PollError.InvalidOption => BadRequest(ApiResponse<object>.Fail("Lựa chọn không hợp lệ.")),
        PollError.Recalled => BadRequest(ApiResponse<object>.Fail("Bình chọn đã bị thu hồi.")),
        _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Lỗi không xác định."))
    };
}