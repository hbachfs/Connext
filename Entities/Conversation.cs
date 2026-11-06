namespace Connext.Api.Entities;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsGroup { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}