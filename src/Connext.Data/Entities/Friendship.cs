using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Connext.Data.Entities;

public class Friendship
{
    [Key]
    public Guid Id { get; set; }

    public Guid RequesterId { get; set; }
    [ForeignKey("RequesterId")]
    public virtual User Requester { get; set; } = null!;

    public Guid AddresseeId { get; set; }
    [ForeignKey("AddresseeId")]
    public virtual User Addressee { get; set; } = null!;

    public Guid PairUserLowId { get; set; }
    public Guid PairUserHighId { get; set; }

    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
