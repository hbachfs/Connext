// src/Connext.Data/Entities/MessageReaction.cs
namespace Connext.Data.Entities;

public class MessageReaction
{
    public long MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Emoji { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}