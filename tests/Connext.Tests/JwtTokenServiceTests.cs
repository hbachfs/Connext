using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Connext.Api.Services;
using Connext.Data.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Connext.Tests;

public class JwtTokenServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly JwtTokenService _jwtTokenService;

    public JwtTokenServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"JwtSettings:SecretKey", "SuperSecretKeyForTestingJwtToken1234567890!"},
            {"JwtSettings:Issuer", "ConnextApi"},
            {"JwtSettings:Audience", "ConnextWeb"},
            {"JwtSettings:ExpiryMinutes", "60"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtTokenService = new JwtTokenService(_configuration);
    }

    [Fact]
    public void GenerateToken_ValidUser_ReturnsValidJwtWithCorrectClaims()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0901234567",
            DisplayName = "Nguyen Van A",
            PasswordHash = "hashedpassword"
        };

        // Act
        var tokenString = _jwtTokenService.GenerateToken(user);

        // Assert
        Assert.NotNull(tokenString);
        Assert.NotEmpty(tokenString);

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(tokenString));

        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Equal("ConnextApi", jwt.Issuer);
        Assert.Contains("ConnextWeb", jwt.Audiences);

        var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(idClaim);
        Assert.Equal(user.Id.ToString(), idClaim.Value);

        var phoneClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.MobilePhone);
        Assert.NotNull(phoneClaim);
        Assert.Equal(user.PhoneNumber, phoneClaim.Value);

        var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal(user.DisplayName, nameClaim.Value);
    }

    [Fact]
    public void PasswordResetToken_UsesSeparateAudienceAndValidatesOnlyAsResetGrant()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+84901234567",
            DisplayName = "Test",
            PasswordHash = "hashed"
        };
        var token = _jwtTokenService.GeneratePasswordResetToken(
            user, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(5));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains("ConnextPasswordReset", jwt.Audiences);
        Assert.DoesNotContain("ConnextWeb", jwt.Audiences);
        Assert.NotNull(_jwtTokenService.ValidatePasswordResetToken(token));
    }
}
