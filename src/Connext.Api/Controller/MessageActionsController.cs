
using Connext.Api.Hubs;
using Connext.Api.Services;
using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class MessageActionsController(MessageActionService svc, IHubContext<ChatHub> hub) : ControllerBase
{
    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task PushAsync(IEnumerable<Guid> userIds, string evt, object payload) =>
        hub.Clients.Groups(userIds.Select(ChatHub.UserGroup).ToList()).SendAsync(evt, payload);

    private IActionResult MapError(MsgError e) => e switch
    {
        MsgError.NotFound => NotFound(ApiResponse<object>.Fail("Không tìm thấy tin nhắn.")),
        MsgError.NotSender => StatusCode(403, ApiResponse<object>.Fail("Chỉ người gửi mới được thu hồi tin nhắn.")),
        MsgError.WindowExpired => BadRequest(ApiResponse<object>.Fail("Đã quá thời gian cho phép thu hồi tin nhắn.")),
        MsgError.AlreadyRecalled => BadRequest(ApiResponse<object>.Fail("Tin nhắn đã được thu hồi.")),
        MsgError.Recalled => BadRequest(ApiResponse<object>.Fail("Tin nhắn đã thu hồi.")),
        MsgError.InvalidEmoji => BadRequest(ApiResponse<object>.Fail("Reaction không hợp lệ.")),
        MsgError.PinLimit => BadRequest(ApiResponse<object>.Fail($"Mỗi hội thoại chỉ ghim tối đa {MessageActionService.MaxPins} tin nhắn.")),
        MsgError.NotAllowed => StatusCode(403, ApiResponse<object>.Fail("Chỉ quản trị viên nhóm mới được ghim tin nhắn.")),
        _ => BadRequest(ApiResponse<object>.Fail("Yêu cầu không hợp lệ."))
    };

    // Thêm hoặc đổi reaction
    [HttpPut("messages/{messageId:long}/reaction")]
    public async Task<IActionResult> SetReaction(long messageId, [FromBody] ReactionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await svc.SetReactionAsync(userId, messageId, req.Emoji, ct);
        if (!r.Ok) return MapError(r.Error);

        await PushAsync(r.MemberIds, "MessageReacted",
            new { ConversationId = r.ConversationId, MessageId = messageId, UserId = userId, Emoji = (string?)req.Emoji });
        return Ok(ApiResponse<object>.Ok(null, "Đã thả reaction."));
    }

    // Bỏ reaction
    [HttpDelete("messages/{messageId:long}/reaction")]
    public async Task<IActionResult> RemoveReaction(long messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await svc.RemoveReactionAsync(userId, messageId, ct);
        if (!r.Ok) return MapError(r.Error);

        await PushAsync(r.MemberIds, "MessageReacted",
            new { ConversationId = r.ConversationId, MessageId = messageId, UserId = userId, Emoji = (string?)null });
        return Ok(ApiResponse<object>.Ok(null, "Đã bỏ reaction."));
    }

    // Thu hồi tin nhắn
    [HttpPost("messages/{messageId:long}/recall")]
    public async Task<IActionResult> Recall(long messageId, CancellationToken ct)
    {
        var r = await svc.RecallAsync(GetCurrentUserId(), messageId, ct);
        if (!r.Ok) return MapError(r.Error);

        await PushAsync(r.MemberIds, "MessageRecalled",
            new { ConversationId = r.ConversationId, MessageId = messageId, Content = MessageMapper.RecalledText });
        return Ok(ApiResponse<object>.Ok(null, "Đã thu hồi tin nhắn."));
    }

    // Ghim
    [HttpPut("conversations/{conversationId:guid}/pins/{messageId:long}")]
    public async Task<IActionResult> Pin(Guid conversationId, long messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await svc.PinAsync(userId, conversationId, messageId, ct);
        if (!r.Ok) return MapError(r.Error);

        await PushAsync(r.MemberIds, "PinsChanged",
            new { ConversationId = conversationId, MessageId = messageId, Pinned = true, ByUserId = userId });
        return Ok(ApiResponse<object>.Ok(null, "Đã ghim tin nhắn."));
    }

    // Bỏ ghim
    [HttpDelete("conversations/{conversationId:guid}/pins/{messageId:long}")]
    public async Task<IActionResult> Unpin(Guid conversationId, long messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var r = await svc.UnpinAsync(userId, conversationId, messageId, ct);
        if (!r.Ok) return MapError(r.Error);

        await PushAsync(r.MemberIds, "PinsChanged",
            new { ConversationId = conversationId, MessageId = messageId, Pinned = false, ByUserId = userId });
        return Ok(ApiResponse<object>.Ok(null, "Đã bỏ ghim."));
    }

    // Danh sách tin đã ghim
    [HttpGet("conversations/{conversationId:guid}/pins")]
    public async Task<IActionResult> GetPins(Guid conversationId, CancellationToken ct)
    {
        var pins = await svc.GetPinsAsync(GetCurrentUserId(), conversationId, ct);
        return pins is null
            ? NotFound(ApiResponse<object>.Fail("Không tìm thấy hội thoại."))
            : Ok(ApiResponse<List<PinnedMessageDto>>.Ok(pins));
    }
}