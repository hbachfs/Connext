using Connext.Shared.DTOs.Auth;

namespace Connext.Api.Services;

public interface IPasswordResetService
{
    Task RequestOtpAsync(string email);
    Task<string?> VerifyOtpAsync(string email, string code);
    Task<bool> ResetPasswordAsync(string token, string newPassword);
}
