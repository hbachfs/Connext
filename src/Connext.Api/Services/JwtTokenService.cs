using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Connext.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Connext.Api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone, user.PhoneNumber),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(jwtSettings["ExpiryMinutes"] ?? "1440")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GeneratePasswordResetToken(User user, string tokenId, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var (jwtSettings, credentials) = GetSigningSettings();
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: GetPasswordResetAudience(jwtSettings),
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, tokenId),
                new Claim("purpose", "reset-password")
            ],
            expires: expiresAt,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidatePasswordResetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var (jwtSettings, credentials) = GetSigningSettings();
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = GetPasswordResetAudience(jwtSettings),
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = credentials.Key,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            }, out _);

            return principal.FindFirst("purpose")?.Value == "reset-password"
                ? principal
                : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private (IConfigurationSection Settings, SigningCredentials Credentials) GetSigningSettings()
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        return (jwtSettings, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    }

    private static string GetPasswordResetAudience(IConfigurationSection settings) =>
        settings["PasswordResetAudience"] ?? "ConnextPasswordReset";
}
