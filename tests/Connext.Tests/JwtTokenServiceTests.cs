using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Connext.Api.Services;
using Connext.Data.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Connext.Tests;

public class JwtTokenServiceTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;

    public JwtTokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            SecretKey = "SuperSecretKeyForTestingJwtTokenGeneration123456!",
            Issuer = "ConnextTestIssuer",
            Audience = "ConnextTestAudience",
            ExpiryMinutes = 60
        };

        _jwtTokenService = new JwtTokenService(Options.Create(_jwtSettings));
    }

    [Fact]
    public void GenerateToken_ShouldContainThreeRequiredClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0987654321",
            DisplayName = "Nguyen Van A"
        };

        var tokenString = _jwtTokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        var nameIdClaim = jwtToken.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid");
        Assert.NotNull(nameIdClaim);
        Assert.Equal(user.Id.ToString(), nameIdClaim.Value);

        var phoneClaim = jwtToken.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.MobilePhone || c.Type == "mobilephone");
        Assert.NotNull(phoneClaim);
        Assert.Equal(user.PhoneNumber, phoneClaim.Value);

        var nameClaim = jwtToken.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Name || c.Type == "unique_name");
        Assert.NotNull(nameClaim);
        Assert.Equal(user.DisplayName, nameClaim.Value);
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectIssuerAudienceAndExpiry()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0987654321",
            DisplayName = "Nguyen Van A"
        };
        var beforeTime = DateTime.UtcNow;

        var tokenString = _jwtTokenService.GenerateToken(user);
        var afterTime = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.Equal(_jwtSettings.Issuer, jwtToken.Issuer);
        Assert.Contains(_jwtSettings.Audience, jwtToken.Audiences);

        var expectedMinExpiry = beforeTime.AddMinutes(_jwtSettings.ExpiryMinutes);
        var expectedMaxExpiry = afterTime.AddMinutes(_jwtSettings.ExpiryMinutes);

        Assert.True(jwtToken.ValidTo >= expectedMinExpiry.AddSeconds(-2));
        Assert.True(jwtToken.ValidTo <= expectedMaxExpiry.AddSeconds(2));
    }
}
