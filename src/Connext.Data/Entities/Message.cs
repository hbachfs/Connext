using Connext.Shared.DTOs.Conversations;

namespace Connext.Data.Entities;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MessageReceipt> Receipts { get; set; } = [];
}
