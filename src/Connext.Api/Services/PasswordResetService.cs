using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(10);
    private const int MaxFailedAttempts = 5;

    private readonly AppDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        AppDbContext context,
        IJwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RequestOtpAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail);
        if (user is null)
            return;

        var latestOtp = await _context.PasswordResetOtps
            .Where(otp => otp.UserId == user.Id && otp.ConsumedAt == null)
            .OrderByDescending(otp => otp.CreatedAt)
            .FirstOrDefaultAsync();
        var now = DateTime.UtcNow;
        if (latestOtp is not null && now - latestOtp.CreatedAt < ResendCooldown)
            return;
        if (latestOtp is not null)
            latestOtp.ConsumedAt = now;

        var otp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = now.Add(OtpLifetime)
        };
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        otp.CodeHash = HashOtp(otp.Id, code);
        _context.PasswordResetOtps.Add(otp);
        await _context.SaveChangesAsync();

        try
        {
            await _emailSender.SendPasswordResetCodeAsync(normalizedEmail, code);
        }
        catch (InvalidOperationException exception)
        {
            otp.ConsumedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogError(exception, "Password reset email could not be sent because SMTP is not configured.");
        }
        catch (System.Net.Mail.SmtpException exception)
        {
            otp.ConsumedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogError(exception, "Password reset email could not be delivered.");
        }
    }

    public async Task<string?> VerifyOtpAsync(string email, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || code.Any(character => !char.IsDigit(character)))
            return null;

        var normalizedEmail = NormalizeEmail(email);
        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail);
        if (user is null)
            return null;

        var now = DateTime.UtcNow;
        var otp = await _context.PasswordResetOtps
            .Where(candidate => candidate.UserId == user.Id && candidate.ConsumedAt == null)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync();
        if (otp is null || otp.ExpiresAt <= now || otp.FailedAttempts >= MaxFailedAttempts)
            return null;

        var expectedHash = Convert.FromHexString(otp.CodeHash);
        var providedHash = Convert.FromHexString(HashOtp(otp.Id, code));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= MaxFailedAttempts)
                otp.ConsumedAt = now;
            await _context.SaveChangesAsync();
            return null;
        }

        otp.ConsumedAt = now;
        var grant = new PasswordResetGrant
        {
            TokenId = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            ExpiresAt = now.Add(GrantLifetime)
        };
        _context.PasswordResetGrants.Add(grant);
        await _context.SaveChangesAsync();
        return _jwtTokenService.GeneratePasswordResetToken(user, grant.TokenId, grant.ExpiresAt);
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return false;

        var principal = _jwtTokenService.ValidatePasswordResetToken(token);
        var tokenId = principal?.FindFirstValue("jti");
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(tokenId))
            return false;

        var grant = await _context.PasswordResetGrants
            .FirstOrDefaultAsync(candidate => candidate.TokenId == tokenId &&
                candidate.UserId == userId &&
                candidate.ConsumedAt == null &&
                candidate.ExpiresAt > DateTime.UtcNow);
        if (grant is null)
            return false;

        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return false;

        grant.ConsumedAt = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    private string HashOtp(Guid otpId, string code)
    {
        var secret = _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes($"{otpId:N}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(key, data));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
