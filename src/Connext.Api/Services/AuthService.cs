using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Auth;
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

        // Kiểm tra số điện thoại đã tồn tại chưa
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Số điện thoại đã được đăng ký.");
        }

        // Tạo user mới
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = request.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Tạo JWT token
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

        // Tìm user theo số điện thoại
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);

        if (user is null)
        {
            return null; // Không tìm thấy user
        }

        // Xác minh mật khẩu
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null; // Sai mật khẩu
        }

        // Cập nhật trạng thái online
        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Tạo JWT token
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
}

