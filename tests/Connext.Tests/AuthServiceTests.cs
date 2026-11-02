using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Connext.Tests;

public class AuthServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private IJwtTokenService CreateJwtTokenService()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"JwtSettings:SecretKey", "SuperSecretKeyForTestingJwtToken1234567890!"},
            {"JwtSettings:Issuer", "ConnextApi"},
            {"JwtSettings:Audience", "ConnextWeb"},
            {"JwtSettings:ExpiryMinutes", "60"}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        return new JwtTokenService(config);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAndReturnsAuthResponse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var jwtService = CreateJwtTokenService();
        var authService = new AuthService(context, jwtService);

        var request = new RegisterRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "MySecurePassword123!",
            DisplayName = "Test User"
        };

        // Act
        var response = await authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Token);
        Assert.Equal(request.PhoneNumber, response.PhoneNumber);
        Assert.Equal(request.DisplayName, response.DisplayName);

        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        Assert.NotNull(savedUser);
        Assert.Equal(request.DisplayName, savedUser.DisplayName);
        Assert.NotEqual(request.Password, savedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, savedUser.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_DuplicatePhoneNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var jwtService = CreateJwtTokenService();
        var authService = new AuthService(context, jwtService);

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0987654321",
            DisplayName = "Existing User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var duplicateRequest = new RegisterRequestDto
        {
            PhoneNumber = "0987654321",
            Password = "AnotherPassword456!",
            DisplayName = "Duplicate User"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authService.RegisterAsync(duplicateRequest));

        Assert.Equal("Số điện thoại đã được đăng ký.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponseAndSetsIsOnline()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var jwtService = CreateJwtTokenService();
        var authService = new AuthService(context, jwtService);

        var plainPassword = "MySecretPassword123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0912345678",
            DisplayName = "Nguyen Van B",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            IsOnline = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loginRequest = new LoginRequestDto
        {
            PhoneNumber = "0912345678",
            Password = plainPassword
        };

        // Act
        var response = await authService.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Token);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(user.PhoneNumber, response.PhoneNumber);

        var updatedUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.IsOnline);
        Assert.NotNull(updatedUser.LastSeen);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var jwtService = CreateJwtTokenService();
        var authService = new AuthService(context, jwtService);

        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0912345678",
            DisplayName = "Nguyen Van B",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
            IsOnline = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loginRequest = new LoginRequestDto
        {
            PhoneNumber = "0912345678",
            Password = "WrongPassword999!"
        };

        // Act
        var response = await authService.LoginAsync(loginRequest);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var jwtService = CreateJwtTokenService();
        var authService = new AuthService(context, jwtService);

        var loginRequest = new LoginRequestDto
        {
            PhoneNumber = "0999999999",
            Password = "SomePassword123!"
        };

        // Act
        var response = await authService.LoginAsync(loginRequest);

        // Assert
        Assert.Null(response);
    }
}

