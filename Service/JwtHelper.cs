using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Connext.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Connext.Api.Services;

/// <summary>Dữ liệu lấy ra từ reset token SAU KHI đã kiểm tra chữ ký.</summary>
public sealed record ResetTokenPayload(Guid UserId, Guid OtpId);

public class JwtHelper
{
    public const string ResetPurpose = "ResetPassword";
    private const string DefaultResetAudience = "connext-password-reset";

    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    public JwtHelper(IConfiguration config, TimeProvider time)
    {
        _config = config;
        _time = time;
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.MobilePhone, user.Phone),
            new Claim("Purpose", "Auth")
        };
        return CreateToken(claims, _config["Jwt:Issuer"], _config["Jwt:Audience"],
            AccessKey(), _config.GetValue<int>("Jwt:ExpireMinutes"));
    }

    // ------------------------------------------------------------------------------------------
    // Reset token: chỉ cấp sau khi nhập ĐÚNG OTP. Khác access token ở 3 điểm:
    //   - khóa ký riêng (dẫn xuất từ Jwt:Key)  - audience riêng  - gắn với 1 bản ghi OTP (dùng 1 lần)
    // Nên reset token không dùng được để gọi API [Authorize], và ngược lại.
    // ------------------------------------------------------------------------------------------
    public string GenerateResetToken(Guid userId, Guid otpId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("otp", otpId.ToString()),
            new Claim("Purpose", ResetPurpose)
        };
        return CreateToken(claims, ResetIssuer(), ResetAudience(), ResetKey(),
            _config.GetValue("Jwt:ResetExpireMinutes", 10));
    }

    /// <summary>
    /// Kiểm tra ĐẦY ĐỦ: chữ ký HS256, issuer, audience, hạn dùng, purpose.
    /// Trả null nếu có bất kỳ điều gì không đúng (không nói rõ lý do).
    /// </summary>
    public ResetTokenPayload? ValidateResetToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;

        var skew = TimeSpan.FromSeconds(30);
        var parameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = ResetKey(),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // chặn alg=none / đổi thuật toán

            ValidateIssuer = true,
            ValidIssuer = ResetIssuer(),
            ValidateAudience = true,
            ValidAudience = ResetAudience(),

            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = skew,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                if (expires is null) return false;
                var now = _time.GetUtcNow().UtcDateTime;
                if (notBefore is not null && now + skew < notBefore.Value) return false;
                return now - skew < expires.Value;
            }
        };

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out _);

            if (principal.FindFirst("Purpose")?.Value != ResetPurpose) return null;
            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return null;
            if (!Guid.TryParse(principal.FindFirst("otp")?.Value, out var otpId)) return null;

            return new ResetTokenPayload(userId, otpId);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    // ---------------------------------------------------------------------------------------- helpers
    private SymmetricSecurityKey AccessKey()
        => new(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

    // Khóa riêng cho reset token, dẫn xuất từ Jwt:Key bằng HMAC => không cần thêm bí mật mới để cấu hình.
    private SymmetricSecurityKey ResetKey()
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        return new SymmetricSecurityKey(hmac.ComputeHash(Encoding.UTF8.GetBytes("connext/password-reset/v1")));
    }

    private string? ResetIssuer() => _config["Jwt:Issuer"];
    private string ResetAudience() => _config["Jwt:ResetAudience"] ?? DefaultResetAudience;

    private string CreateToken(Claim[] claims, string? issuer, string? audience, SymmetricSecurityKey key, int expireMinutes)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(expireMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
