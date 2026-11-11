namespace Connext.Data.Entities;

public class Message
{
    public long Id { get; set; } // Identity column
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }

    // Type: text, image, file (chữ thường, cùng quy ước với FilesController)
    public string Type { get; set; } = "text";
    public string? Content { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }

    // Status: Sent, Delivered, Seen
    public string Status { get; set; } = "Sent";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime SentAt { get => CreatedAt; set => CreatedAt = value; }
    public DateTime? ReadAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
