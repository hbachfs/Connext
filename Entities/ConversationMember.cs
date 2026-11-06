namespace Connext.Api.Entities;

public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }

    // Role: Admin, Member
    public required string Role { get; set; }
    public bool IsPinned { get; set; }

    // Dùng cho Soft Delete / Ẩn hội thoại
    public DateTime? ClearedAt { get; set; }
    public long LastReadMessageId { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}