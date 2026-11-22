namespace Connext.Data.Entities;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public bool IsGroup { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ConversationMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
