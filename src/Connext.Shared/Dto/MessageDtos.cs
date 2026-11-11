namespace Connext.Shared.Chat;

public class MessageDto
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public bool Mine { get; set; } // True nếu là tin nhắn do user hiện tại gửi
    public required string Type { get; set; } // "text", "image", "file"
    public string? Content { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Status { get; set; } // "Sent", "Delivered", "Seen"
}

public record PagedMessagesResponse(
    IReadOnlyList<MessageDto> Items, long? NextBeforeId);
