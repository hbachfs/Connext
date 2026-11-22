// src/Connext.Data/Entities/MessagePin.cs
namespace Connext.Data.Entities;

public class MessagePin
{
    public long MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid ConversationId { get; set; }   // cột thường, không FK (tránh multiple cascade path trên SQL Server)

    public Guid PinnedById { get; set; }
    public User PinnedBy { get; set; } = null!;

    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}