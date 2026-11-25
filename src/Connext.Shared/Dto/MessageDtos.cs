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

    public List<Guid> MentionedUserIds { get; set; } = new List<Guid>();

    
    public bool IsRecalled { get; set; }
    public long? ReplyToMessageId { get; set; }
    public ReplyPreviewDto? ReplyTo { get; set; }   // nội dung tin gốc rút gọn
    public List<ReactionDto> Reactions { get; set; } = new List<ReactionDto>();
    public PollDto? Poll { get; set; }
}


public class ReplyPreviewDto
{
    public long Id { get; set; }
    public Guid SenderId { get; set; }
    public string? Preview { get; set; }
}


public class ReactionDto
{
    public string Emoji { get; set; } = "";
    public int Count { get; set; }
    public List<Guid> UserIds { get; set; } = new List<Guid>();
}


public class PinnedMessageDto
{
    public long MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string Type { get; set; } = "text";
    public string? Preview { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid PinnedById { get; set; }
    public DateTime PinnedAt { get; set; }
}


public record ReactionRequest(string Emoji);

public record PagedMessagesResponse(
    IReadOnlyList<MessageDto> Items, long? NextBeforeId);
