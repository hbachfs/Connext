using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Connext.Api.Entities;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Connext.Api.Tests;

/// <summary>Kiểm tra reset token: đặc biệt là TỪ CHỐI TOKEN GIẢ (lỗ hổng ReadJwtToken trong issue).</summary>
public class ResetTokenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeTime _time = new(T0);
    private readonly Guid _uid = Guid.NewGuid();
    private readonly Guid _otp = Guid.NewGuid();

    private string Forge(byte[] key, string? aud, string? iss, string purpose = "ResetPassword", string alg = SecurityAlgorithms.HmacSha256)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, "victim@x.com"),   // kịch bản trong issue
            new Claim("Purpose", purpose),
            new Claim("sub", _uid.ToString()),
            new Claim("otp", _otp.ToString()),
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(key), alg);
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(iss, aud, claims, T0.UtcDateTime, T0.UtcDateTime.AddMinutes(15), creds));
    }

    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    // Đúng khóa dẫn xuất mà JwtHelper dùng để ký/kiểm reset token. Dùng khóa này để giả mạo thì chỉ còn 1 thứ sai (purpose, thuật toán...) => test mới thật sự kiểm tra thứ đó.
    private static byte[] ResetKey()
    {
        using var h = new HMACSHA256(U(TestConfig.Key));
        return h.ComputeHash(U("connext/password-reset/v1"));
    }

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Valid_token_is_accepted()
    {
        var jwt = TestConfig.Jwt(_time);
        var p = jwt.ValidateResetToken(jwt.GenerateResetToken(_uid, _otp));
        Assert.NotNull(p);
        Assert.Equal(_uid, p!.UserId);
        Assert.Equal(_otp, p.OtpId);
    }

    [Fact]
    public void Forge_helper_with_the_real_key_is_accepted()   // đối chứng: helper giả mạo đúng định dạng => các test từ chối bên dưới mới có ý nghĩa
    {
        var token = Forge(ResetKey(), "connext-password-reset", "connext");
        Assert.NotNull(TestConfig.Jwt(_time).ValidateResetToken(token));
    }

    [Fact]
    public void Forged_token_with_attacker_key_is_rejected()
        => Assert.Null(TestConfig.Jwt(_time).ValidateResetToken(
            Forge(U("attacker-key-attacker-key-attacker-key!!"), "connext-password-reset", "connext")));

    [Fact]
    public void Forged_token_signed_with_the_access_key_is_rejected()   // kể cả khi kẻ tấn công biết Jwt:Key
        => Assert.Null(TestConfig.Jwt(_time).ValidateResetToken(Forge(U(TestConfig.Key), "connext-password-reset", "connext")));

    [Fact]
    public void Unsigned_alg_none_token_is_rejected()
    {
        var token = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
                    B64($"{{\"sub\":\"{_uid}\",\"otp\":\"{_otp}\",\"Purpose\":\"ResetPassword\",\"iss\":\"connext\",\"aud\":\"connext-password-reset\",\"exp\":4102444800}}") + ".";
        Assert.Null(TestConfig.Jwt(_time).ValidateResetToken(token));
    }

    [Fact]
    public void Tampered_payload_with_original_signature_is_rejected()
    {
        var jwt = TestConfig.Jwt(_time);
        var parts = jwt.GenerateResetToken(_uid, _otp).Split('.');
        var evil = B64($"{{\"sub\":\"{Guid.NewGuid()}\",\"otp\":\"{_otp}\",\"Purpose\":\"ResetPassword\",\"iss\":\"connext\",\"aud\":\"connext-password-reset\",\"exp\":4102444800}}");
        Assert.Null(jwt.ValidateResetToken($"{parts[0]}.{evil}.{parts[2]}"));
    }

    [Fact]
    public void Access_token_cannot_be_used_as_reset_token()
    {
        var jwt = TestConfig.Jwt(_time);
        var access = jwt.GenerateToken(new User { FullName = "A", Phone = "0900000000", Email = "a@x.com", PasswordHash = "x" });
        Assert.Null(jwt.ValidateResetToken(access));
    }

    [Fact]
    public void Reset_token_cannot_be_used_as_access_token()
    {
        var jwt = TestConfig.Jwt(_time);
        var reset = jwt.GenerateResetToken(_uid, _otp);
        var bearer = new TokenValidationParameters   // đúng tham số JwtBearer trong Program.cs
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "connext",
            ValidAudience = "connext-clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConfig.Key)),
        };
        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(reset, bearer, out _));
    }

    [Fact]
    public void Wrong_purpose_is_rejected()
    {
        var token = Forge(
            Encoding.UTF8.GetBytes(TestConfig.Key),
            "wrong-purpose",
            "connext"
        );

        Assert.Null(
            TestConfig.Jwt(_time).ValidateResetToken(token)
        );
    }

    [Fact]
    public void Different_algorithm_is_rejected()
    {
       
        var hs512Key = RandomNumberGenerator.GetBytes(64);

        
        var token = Forge(
            hs512Key,
            "connext-password-reset",
            "connext",
            alg: SecurityAlgorithms.HmacSha512
        );

        Assert.Null(
            TestConfig.Jwt(_time).ValidateResetToken(token)
        );
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("a.b.c")]
    public void Garbage_is_rejected(string? token) => Assert.Null(TestConfig.Jwt(_time).ValidateResetToken(token));

    [Fact]
    public void Expired_token_is_rejected()
    {
        var jwt = TestConfig.Jwt(_time);
        var token = jwt.GenerateResetToken(_uid, _otp);
        _time.Now = T0.AddMinutes(11);
        Assert.Null(jwt.ValidateResetToken(token));
    }
}