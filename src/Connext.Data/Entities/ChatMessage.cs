namespace Connext.Data.Entities;

public class ChatMessage
{
    public long Id { get; set; }
    public string SenderId { get; set; } = null!;
    public string ReceiverId { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsSeen { get; set; }
}