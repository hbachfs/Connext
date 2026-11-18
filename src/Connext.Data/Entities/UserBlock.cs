
namespace Connext.Data.Entities;

public class UserBlock
{
    public int BlockerId { get; set; }
    public User Blocker { get; set; } = null!;

    public int BlockedUserId { get; set; }
    public User BlockedUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}