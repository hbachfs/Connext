namespace Connext.Api.Entities;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }

    // Status: Pending, Accepted
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Requester { get; set; } = null!;
    public User Addressee { get; set; } = null!;
}
