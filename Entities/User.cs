namespace Connext.Api.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public required string Phone { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? Gender { get; set; } // Nam, Nữ, Khác
    public string? AvatarUrl { get; set; }

    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Friendship> SentFriendRequests { get; set; } = new List<Friendship>();
    public ICollection<Friendship> ReceivedFriendRequests { get; set; } = new List<Friendship>();
    public ICollection<ConversationMember> ConversationMembers { get; set; } = new List<ConversationMember>();
}