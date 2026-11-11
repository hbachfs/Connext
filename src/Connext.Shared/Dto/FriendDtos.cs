namespace Connext.Shared.Dto;

public class FriendDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

public class FriendRequestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int MutualFriendsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendFriendRequestDto
{
    public string Phone { get; set; } = string.Empty;
}

