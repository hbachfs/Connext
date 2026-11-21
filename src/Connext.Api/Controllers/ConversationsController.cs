using System.Security.Claims;
using System.Net.Http.Headers;
using Connext.Api.Services;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly IConversationFileService _fileService;

    public ConversationsController(
        IConversationService conversationService,
        IConversationFileService fileService)
    {
        _conversationService = conversationService;
        _fileService = fileService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetConversations() =>
        Ok(await _conversationService.GetConversationsAsync(CurrentUserId));

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupConversationDto request)
    {
        try
        {
            var conversation = await _conversationService.CreateGroupAsync(CurrentUserId, request);
            return CreatedAtAction(nameof(GetConversations), conversation);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

        [HttpPost("direct/{friendId:guid}")]
        public async Task<IActionResult> CreateDirect(Guid friendId)
        {
            try
            {
                var conversation = await _conversationService.CreateDirectAsync(CurrentUserId, friendId);
                return Ok(conversation);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("{conversationId:guid}/members")]
        public async Task<IActionResult> AddMember(Guid conversationId, [FromBody] AddConversationMemberDto request)
        {
            try { await _conversationService.AddMemberAsync(CurrentUserId, conversationId, request.UserId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpDelete("{conversationId:guid}/members/{memberId:guid}")]
        public async Task<IActionResult> RemoveMember(Guid conversationId, Guid memberId)
        {
            try { await _conversationService.RemoveMemberAsync(CurrentUserId, conversationId, memberId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{conversationId:guid}/leave")]
        public async Task<IActionResult> LeaveGroup(Guid conversationId)
        {
            try { await _conversationService.LeaveGroupAsync(CurrentUserId, conversationId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPut("{conversationId:guid}")]
        public async Task<IActionResult> RenameGroup(Guid conversationId, [FromBody] RenameConversationDto request)
        {
            try { await _conversationService.RenameGroupAsync(CurrentUserId, conversationId, request.Name); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPut("{conversationId:guid}/members/{memberId:guid}/role")]
        public async Task<IActionResult> ChangeMemberRole(Guid conversationId, Guid memberId, [FromBody] ChangeMemberRoleDto request)
        {
            try { await _conversationService.ChangeMemberRoleAsync(CurrentUserId, conversationId, memberId, request.Role); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpDelete("{conversationId:guid}/messages/{messageId:guid}")]
        public async Task<IActionResult> RecallMessage(Guid conversationId, Guid messageId)
        {
            try { await _conversationService.RecallMessageAsync(CurrentUserId, conversationId, messageId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{conversationId:guid}/messages/{messageId:guid}/reactions")]
        public async Task<IActionResult> AddReaction(Guid conversationId, Guid messageId, [FromBody] AddMessageReactionDto request)
        {
            try { await _conversationService.AddReactionAsync(CurrentUserId, conversationId, messageId, request.Emoji); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/reactions")]
        public async Task<IActionResult> RemoveReaction(Guid conversationId, Guid messageId, [FromQuery] string emoji)
        {
            try { await _conversationService.RemoveReactionAsync(CurrentUserId, conversationId, messageId, emoji); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{conversationId:guid}/messages/{messageId:guid}/pin")]
        public async Task<IActionResult> PinMessage(Guid conversationId, Guid messageId)
        {
            try { await _conversationService.PinMessageAsync(CurrentUserId, conversationId, messageId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/pin")]
        public async Task<IActionResult> UnpinMessage(Guid conversationId, Guid messageId)
        {
            try { await _conversationService.UnpinMessageAsync(CurrentUserId, conversationId, messageId); return NoContent(); }
            catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpGet("{conversationId:guid}/pins")]
        public async Task<IActionResult> GetPinnedMessages(Guid conversationId)
        {
            try { return Ok(await _conversationService.GetPinnedMessagesAsync(CurrentUserId, conversationId)); }
            catch (InvalidOperationException exception) { return NotFound(new { message = exception.Message }); }
        }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] int pageSize = 50)
    {
        try
        {
            return Ok(await _conversationService.GetMessagesAsync(CurrentUserId, conversationId, pageSize));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid conversationId)
    {
        try
        {
            await _conversationService.MarkReadAsync(CurrentUserId, conversationId);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost("{conversationId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ConversationFileService.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConversationFileService.MaxRequestBytes)]
    public async Task<IActionResult> UploadFile(
        Guid conversationId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var uploaded = await _fileService.UploadAsync(
                CurrentUserId, conversationId, file, cancellationToken);
            return Created(uploaded.DownloadUrl, uploaded);
        }
        catch (ConversationFileValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{conversationId:guid}/media")]
    public async Task<IActionResult> GetMedia(Guid conversationId, [FromQuery] string? kind)
    {
        try
        {
            return Ok(await _fileService.GetMediaAsync(CurrentUserId, conversationId, kind));
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{conversationId:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> DownloadFile(
        Guid conversationId,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var file = await _fileService.GetDownloadInfoAsync(CurrentUserId, conversationId, fileId);
        if (file is null)
            return NotFound();

        Response.ContentType = "application/octet-stream";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.ContentLength = file.Value.Size;
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = file.Value.FileName
        }.ToString();
        await _fileService.DownloadAsync(
            CurrentUserId, conversationId, fileId, Response.Body, cancellationToken);
        return new EmptyResult();
    }
}
