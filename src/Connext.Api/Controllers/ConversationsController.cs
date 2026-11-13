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
