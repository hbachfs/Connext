using System.ComponentModel.DataAnnotations;

namespace Connext.Api.Dto;

public class RegisterRequest
{
    [Required] public string FullName { get; set; } = null!;
    [Required] public string Phone { get; set; } = null!;
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required, MinLength(6)] public string Password { get; set; } = null!;
    [Compare("Password")] public string ConfirmPassword { get; set; } = null!;
}

public class LoginRequest
{
    [Required] public string Phone { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
}

public class VerifyOtpRequest
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required, StringLength(6, MinimumLength = 6)] public string Otp { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [Required] public string ResetToken { get; set; } = null!; // JWT ngắn hạn
    [Required, MinLength(6)] public string NewPassword { get; set; } = null!;
    [Compare("NewPassword")] public string ConfirmPassword { get; set; } = null!;
}
