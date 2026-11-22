using Connext.Shared.DTOs.Auth;

namespace Connext.Api.Services;

public interface IUserProfileService
{
    Task<UserProfileDto?> GetAsync(Guid userId);
    Task<UserProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequestDto request);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request);
}
