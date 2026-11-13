using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared;
using Connext.Shared.DTOs.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(AppDbContext dbContext, IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!PhoneNumberNormalizer.TryNormalize(request.PhoneNumber, out var phoneNumber))
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        var email = NormalizeEmail(request.Email);

        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumber ||
                (email != null && user.Email == email));

        if (existingUser is not null)
            throw new InvalidOperationException(existingUser.PhoneNumber == phoneNumber
                ? "Số điện thoại đã được đăng ký."
                : "Email đã được đăng ký.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            throw new InvalidOperationException("Số điện thoại hoặc email đã được đăng ký.", exception);
        }

        var token = _jwtTokenService.GenerateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PhoneNumberNormalizer.TryNormalize(request.PhoneNumber, out var phoneNumber))
            return null;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

        if (user is null)
        {
            return null; // Không tìm thấy user
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null; // Sai mật khẩu
        }

        var token = _jwtTokenService.GenerateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };
    }

    public async Task LogoutAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return;

        user.IsOnline = false;
        user.LastSeen = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
