namespace Connext.Data.Entities;

public static class FriendshipStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
}

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }

    // Status: Pending, Accepted
    public string Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Requester { get; set; } = null!;
    public User Addressee { get; set; } = null!;
}
