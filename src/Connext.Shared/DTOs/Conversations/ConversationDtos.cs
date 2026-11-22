using System.ComponentModel.DataAnnotations;

namespace Connext.Shared.DTOs.Conversations;

public class CreateGroupConversationDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Nhóm cần ít nhất hai thành viên khác người tạo.")]
    public List<Guid> MemberIds { get; set; } = [];

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class ConversationSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public DateTime CreatedAt { get; set; }
    public MessageSummaryDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageSummaryDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageDto
{
    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [EnumDataType(typeof(MessageType))]
    public MessageType Type { get; set; } = MessageType.Text;
}

public class MessageResponseDto : MessageSummaryDto
{
    public string SenderDisplayName { get; set; } = string.Empty;
    public List<Guid> DeliveredToUserIds { get; set; } = [];
}
