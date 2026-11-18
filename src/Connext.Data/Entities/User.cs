using System.ComponentModel.DataAnnotations;

namespace Connext.Data.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string FullName { get; set; }

    [Required]
    [MaxLength(20)]
    public required string Phone { get; set; }

    [Required]
    [MaxLength(256)]
    public required string Email { get; set; }

    [Required]
    public required string PasswordHash { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Friendship> SentFriendRequests { get; set; } = new List<Friendship>();
    public ICollection<Friendship> ReceivedFriendRequests { get; set; } = new List<Friendship>();
    public ICollection<ConversationMember> ConversationMembers { get; set; } = new List<ConversationMember>();
}