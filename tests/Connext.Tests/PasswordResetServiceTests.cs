using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Connext.Tests;

public class PasswordResetServiceTests
{
    private static readonly Dictionary<string, string?> JwtSettings = new()
    {
        ["JwtSettings:SecretKey"] = "LongEnoughPasswordResetTestSecretKey123!",
        ["JwtSettings:Issuer"] = "ConnextApi",
        ["JwtSettings:Audience"] = "ConnextWeb",
        ["JwtSettings:ExpiryMinutes"] = "60"
    };

    [Fact]
    public async Task OtpFlow_RejectsWrongCodeAndForgedToken_AndAllowsGrantOnlyOnce()
    {
        using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var sender = new TestEmailSender();
        var service = CreateService(context, sender);

        await service.RequestOtpAsync(user.Email!);
        Assert.False(string.IsNullOrEmpty(sender.Code));
        var validCode = sender.Code ?? throw new InvalidOperationException("The test code was not delivered.");
        Assert.Null(await service.VerifyOtpAsync(user.Email!, validCode == "000000" ? "000001" : "000000"));

        var token = await service.VerifyOtpAsync(user.Email!, validCode);
        Assert.NotNull(token);
        Assert.False(await service.ResetPasswordAsync(TamperSignature(token), "NewPassword123!"));
        Assert.True(await service.ResetPasswordAsync(token, "NewPassword123!"));
        Assert.False(await service.ResetPasswordAsync(token, "AnotherPassword123!"));
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", user.PasswordHash));
    }

    [Fact]
    public async Task VerifyOtp_LocksCodeAfterFiveIncorrectAttempts()
    {
        using var context = CreateContext();
        var user = CreateUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var sender = new TestEmailSender();
        var service = CreateService(context, sender);

        await service.RequestOtpAsync(user.Email!);
        var wrongCode = sender.Code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Null(await service.VerifyOtpAsync(user.Email!, wrongCode));

        Assert.Null(await service.VerifyOtpAsync(user.Email!, sender.Code!));
    }

    [Fact]
    public async Task ForgotPassword_DoesNotSendCodeForUnknownEmail()
    {
        using var context = CreateContext();
        var sender = new TestEmailSender();
        var service = CreateService(context, sender);

        await service.RequestOtpAsync("not-registered@example.com");

        Assert.Null(sender.Code);
        Assert.Empty(await context.PasswordResetOtps.ToListAsync());
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "person@example.com",
        PhoneNumber = "+84901234567",
        DisplayName = "Test User",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!")
    };

    private static PasswordResetService CreateService(AppDbContext context, TestEmailSender sender)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(JwtSettings).Build();
        var tokenService = new JwtTokenService(configuration);
        return new PasswordResetService(
            context, tokenService, sender, configuration, NullLogger<PasswordResetService>.Instance);
    }

    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        var signature = parts[2];
        parts[2] = (signature[0] == 'A' ? "B" : "A") + signature[1..];
        return string.Join('.', parts);
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public string? Code { get; private set; }

        public Task SendPasswordResetCodeAsync(string email, string code)
        {
            Code = code;
            return Task.CompletedTask;
        }
    }
}
