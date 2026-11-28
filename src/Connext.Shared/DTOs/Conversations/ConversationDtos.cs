using System.ComponentModel.DataAnnotations;

namespace Connext.Shared.DTOs.Conversations;

public class CreateGroupConversationDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Nhóm cần ít nhất hai thành viên khác người tạo.")]
    [MaxLength(49, ErrorMessage = "Nhóm không được vượt quá 50 thành viên, bao gồm người tạo.")]
    public List<Guid> MemberIds { get; set; } = [];

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class ConversationSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
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
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [EnumDataType(typeof(MessageType))]
    public MessageType Type { get; set; } = MessageType.Text;

    public Guid? ReplyToMessageId { get; set; }
    public List<Guid> MentionedUserIds { get; set; } = [];
}

public class CreatePollDto
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(10)]
    public List<string> Options { get; set; } = [];
}

public class CastPollVoteDto
{
    [Required]
    public Guid OptionId { get; set; }
}

public class PollResponseDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TotalVotes { get; set; }
    public Guid? MyVoteOptionId { get; set; }
    public List<PollOptionResultDto> Options { get; set; } = [];
}

public class PollVoteUpdateDto
{
    public PollResponseDto Poll { get; set; } = new();
    public Guid UserId { get; set; }
    public Guid OptionId { get; set; }
}

public class PollOptionResultDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public decimal Percentage { get; set; }
}

public class MessageResponseDto : MessageSummaryDto
{
    public string SenderDisplayName { get; set; } = string.Empty;
    public List<Guid> DeliveredToUserIds { get; set; } = [];
    public Guid? ReplyToMessageId { get; set; }
    public MessageSummaryDto? ReplyToMessage { get; set; }
    public Dictionary<string, List<Guid>> Reactions { get; set; } = [];
    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? EditedAt { get; set; }
    public List<Guid> MentionedUserIds { get; set; } = [];
    public PollResponseDto? Poll { get; set; }
}

public class AddConversationMemberDto
{
    [Required]
    public Guid UserId { get; set; }
}

public class RenameConversationDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

public class ChangeMemberRoleDto
{
    [Required]
    [RegularExpression("Member|Admin")]
    public string Role { get; set; } = "Member";
}

public class UpdateGroupAvatarDto
{
    [StringLength(500)]
    public string? AvatarUrl { get; set; }
}

public class IceServerDto
{
    public string[] Urls { get; set; } = [];
    public string? Username { get; set; }
    public string? Credential { get; set; }
}

public class AddMessageReactionDto
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string Emoji { get; set; } = string.Empty;
}
