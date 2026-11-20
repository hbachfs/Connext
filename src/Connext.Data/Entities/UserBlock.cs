
//using Connext.Data;
namespace Connext.Data.Entities;
public class UserBlock
{
    public Guid BlockerId { get; set; }          // phải là Guid, không phải int
    public User Blocker { get; set; } = null!;

    public Guid BlockedUserId { get; set; }      // phải là Guid, không phải int
    public User BlockedUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}