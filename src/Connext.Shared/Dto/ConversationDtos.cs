namespace Connext.Shared.Dto;

public class ConversationDto
{
    public Guid ConversationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsGroup { get; set; }
    public bool IsOnline { get; set; } // Dùng cho chat 1-1
    public string StatusText { get; set; } = string.Empty; // Trạng thái online hoặc "N thành viên"

    // Tin nhắn cuối
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public string? LastMessageType { get; set; }

    public int UnreadCount { get; set; }
    public bool IsPinned { get; set; }
}

public class CreateGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> MemberIds { get; set; } = new();
}

public class AddMembersRequest
{
    public List<Guid> MemberIds { get; set; } = new();
}

public class PinConversationRequest
{
    public bool IsPinned { get; set; }
}
public class RenameGroupRequest
{
    public string Name { get; set; } = string.Empty;
}
