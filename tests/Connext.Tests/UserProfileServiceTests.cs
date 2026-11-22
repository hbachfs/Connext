using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;

namespace Connext.Tests;

public class UserProfileServiceTests
{
    [Fact]
    public async Task UpdateAsync_NormalizesEmailAndRejectsDuplicateEmail()
    {
        using var context = CreateContext();
        var user = CreateUser("+84901234567", null);
        var other = CreateUser("+84901234568", "other@example.com");
        context.Users.AddRange(user, other);
        await context.SaveChangesAsync();
        var service = new UserProfileService(context);

        var updated = await service.UpdateAsync(user.Id, new UpdateProfileRequestDto
        {
            DisplayName = " Updated ",
            Email = " PERSON@Example.com ",
            AvatarUrl = null
        });
        Assert.Equal("person@example.com", updated!.Email);
        Assert.Equal("Updated", updated.DisplayName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(user.Id,
            new UpdateProfileRequestDto
            {
                DisplayName = "Updated",
                Email = "OTHER@example.com"
            }));
    }

    [Fact]
    public async Task ChangePasswordAsync_RequiresCurrentPassword()
    {
        using var context = CreateContext();
        var user = CreateUser("+84901234567", null);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new UserProfileService(context);

        Assert.False(await service.ChangePasswordAsync(user.Id, new ChangePasswordRequestDto
        {
            CurrentPassword = "IncorrectPassword!",
            NewPassword = "NewPassword123!"
        }));
        Assert.True(await service.ChangePasswordAsync(user.Id, new ChangePasswordRequestDto
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        }));
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", user.PasswordHash));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(string phoneNumber, string? email) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = phoneNumber,
        Email = email,
        DisplayName = "Test",
        PasswordHash = "test-hash"
    };
}
