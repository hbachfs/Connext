using System.ComponentModel.DataAnnotations;

namespace Connext.Shared.DTOs.Contacts;

public class UserSearchResponseDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsFriend { get; set; }
}

public class FriendResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
}

public class FriendshipRequestDto
{
    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class FriendshipRespondDto
{
    public Guid RequestId { get; set; }
    public bool Accept { get; set; }
}
