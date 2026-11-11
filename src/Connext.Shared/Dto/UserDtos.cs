using System.ComponentModel.DataAnnotations;

namespace Connext.Shared.Dto;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? AvatarUrl { get; set; }
}

public class UpdateProfileRequest
{
    [Required] public string FullName { get; set; } = null!;
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required] public string Phone { get; set; } = null!;
    public string? Gender { get; set; }
    public string? AvatarUrl { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = null!;
    [Required, MinLength(6)] public string NewPassword { get; set; } = null!;
    [Compare("NewPassword")] public string ConfirmPassword { get; set; } = null!;
}
