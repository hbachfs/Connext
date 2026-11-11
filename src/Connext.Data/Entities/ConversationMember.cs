
namespace Connext.Data.Entities;

public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }

    // Role: Admin, Member
    public string Role { get; set; } = "Member";
    public bool IsPinned { get; set; }

    // Xóa hội thoại (soft delete) gồm 2 phần độc lập:
    //   - ClearedAt: mốc cắt lịch sử, tin cũ hơn mốc này không bao giờ hiện lại
    //   - IsHidden : ẩn khỏi danh sách; có tin mới thì tự hiện lại (nhưng lịch sử cũ vẫn bị cắt)
    public DateTime? ClearedAt { get; set; }
    public bool IsHidden { get; set; }
    public long LastReadMessageId { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}

