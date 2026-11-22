using Connext.Data;
using Connext.Shared.DTOs.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class UserProfileService : IUserProfileService
{
    private readonly AppDbContext _context;

    public UserProfileService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfileDto?> GetAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return null;

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim().ToLowerInvariant();
        if (email is not null && await _context.Users.AnyAsync(candidate =>
                candidate.Id != userId && candidate.Email == email))
            throw new InvalidOperationException("Email đã được đăng ký.");

        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            _context.Entry(user).State = EntityState.Detached;
            throw new InvalidOperationException("Email đã được đăng ký.", exception);
        }
        return ToDto(user);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await _context.Users.FindAsync(userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    private static UserProfileDto ToDto(Connext.Data.Entities.User user) => new()
    {
        Id = user.Id,
        PhoneNumber = user.PhoneNumber,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl
    };
}
