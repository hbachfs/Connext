using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Connext.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Connext.Api.Services;

public class JwtHelper
{
    private readonly IConfiguration _config;

    public JwtHelper(IConfiguration config)
    {
        _config = config;
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
        return CreateToken(claims, _config.GetValue<int>("Jwt:ExpireMinutes"));
    }

    // Tạo token ngắn hạn (15 phút) chỉ để dùng cho việc đổi mật khẩu sau khi nhập OTP
    public string GenerateResetToken(string email)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("Purpose", "ResetPassword")
        };
        return CreateToken(claims, 15);
    }

    private string CreateToken(Claim[] claims, int expireMinutes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
