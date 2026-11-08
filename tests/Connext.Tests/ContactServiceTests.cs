using System.ComponentModel.DataAnnotations;
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Contacts;
using Microsoft.EntityFrameworkCore;

namespace Connext.Tests;

public class ContactServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(string phoneNumber, string displayName) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = phoneNumber,
        DisplayName = displayName,
        PasswordHash = "test-hash"
    };

    [Fact]
    public async Task SearchUsersAsync_FindsByExactPhoneOrPartialNameAndMarksFriends()
    {
        using var context = CreateInMemoryDbContext();
        var currentUser = CreateUser("0900000001", "Current User");
        var friend = CreateUser("0900000002", "Jane Doe");
        var otherUser = CreateUser("0900000003", "Janet Smith");
        context.Users.AddRange(currentUser, friend, otherUser);
        context.Friendships.Add(new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = currentUser.Id,
            AddresseeId = friend.Id,
            PairUserLowId = GetPairLowId(currentUser.Id, friend.Id),
            PairUserHighId = GetPairHighId(currentUser.Id, friend.Id),
            Requester = currentUser,
            Addressee = friend,
            Status = FriendshipStatus.Accepted
        });
        await context.SaveChangesAsync();

        var service = new ContactService(context);
        var byName = await service.SearchUsersAsync(currentUser.Id, "Jane");
        var byPhone = await service.SearchUsersAsync(currentUser.Id, friend.PhoneNumber);

        Assert.Equal(2, byName.Count);
        Assert.Contains(byName, user => user.Id == friend.Id && user.IsFriend);
        Assert.Contains(byName, user => user.Id == otherUser.Id && !user.IsFriend);
        Assert.Single(byPhone);
        Assert.Equal(friend.Id, byPhone[0].Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SearchUsersAsync_BlankQueryReturnsEmpty(string query)
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateUser("0900000001", "Test User");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new ContactService(context);

        var result = await service.SearchUsersAsync(user.Id, query);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SendFriendRequestAsync_CreatesPendingRequestAndRejectsReverseDuplicate()
    {
        using var context = CreateInMemoryDbContext();
        var requester = CreateUser("0900000001", "Requester");
        var addressee = CreateUser("0900000002", "Addressee");
        context.Users.AddRange(requester, addressee);
        await context.SaveChangesAsync();
        var service = new ContactService(context);

        var sent = await service.SendFriendRequestAsync(requester.Id, $" {addressee.PhoneNumber} ");
        var reverseDuplicate = await service.SendFriendRequestAsync(addressee.Id, requester.PhoneNumber);
        var request = await context.Friendships.SingleAsync();

        Assert.True(sent);
        Assert.False(reverseDuplicate);
        Assert.Equal(requester.Id, request.RequesterId);
        Assert.Equal(addressee.Id, request.AddresseeId);
        Assert.Equal(FriendshipStatus.Pending, request.Status);
        Assert.Equal(GetPairLowId(requester.Id, addressee.Id), request.PairUserLowId);
        Assert.Equal(GetPairHighId(requester.Id, addressee.Id), request.PairUserHighId);
    }

    [Fact]
    public async Task SendFriendRequestAsync_RejectsSelfRequestUnknownPhoneAndBlankPhone()
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateUser("0900000001", "Test User");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new ContactService(context);

        Assert.False(await service.SendFriendRequestAsync(user.Id, user.PhoneNumber));
        Assert.False(await service.SendFriendRequestAsync(user.Id, "0900000099"));
        Assert.False(await service.SendFriendRequestAsync(user.Id, " "));
        Assert.Empty(await context.Friendships.ToListAsync());
    }

    [Fact]
    public async Task RespondToRequestAsync_OnlyAddresseeCanAcceptPendingRequest()
    {
        using var context = CreateInMemoryDbContext();
        var requester = CreateUser("0900000001", "Requester");
        var addressee = CreateUser("0900000002", "Addressee");
        var request = new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = requester.Id,
            AddresseeId = addressee.Id,
            PairUserLowId = GetPairLowId(requester.Id, addressee.Id),
            PairUserHighId = GetPairHighId(requester.Id, addressee.Id),
            Requester = requester,
            Addressee = addressee,
            Status = FriendshipStatus.Pending
        };
        context.Users.AddRange(requester, addressee);
        context.Friendships.Add(request);
        await context.SaveChangesAsync();
        var service = new ContactService(context);

        Assert.False(await service.RespondToRequestAsync(requester.Id, request.Id, accept: true));
        Assert.True(await service.RespondToRequestAsync(addressee.Id, request.Id, accept: true));
        Assert.False(await service.RespondToRequestAsync(addressee.Id, request.Id, accept: false));
        Assert.Equal(FriendshipStatus.Accepted, request.Status);
    }

    [Fact]
    public async Task GetFriendsAsync_ReturnsOnlyAcceptedFriendships()
    {
        using var context = CreateInMemoryDbContext();
        var currentUser = CreateUser("0900000001", "Current User");
        var friend = CreateUser("0900000002", "Friend");
        var pendingUser = CreateUser("0900000003", "Pending User");
        context.Users.AddRange(currentUser, friend, pendingUser);
        context.Friendships.AddRange(
            CreateFriendship(currentUser, friend, FriendshipStatus.Accepted),
            CreateFriendship(currentUser, pendingUser, FriendshipStatus.Pending));
        await context.SaveChangesAsync();

        var service = new ContactService(context);
        var result = await service.GetFriendsAsync(currentUser.Id);

        var friendResult = Assert.Single(result);
        Assert.Equal(friend.Id, friendResult.UserId);
        Assert.Equal(friend.DisplayName, friendResult.DisplayName);
    }

    [Fact]
    public void FriendshipModel_EnforcesUniqueCanonicalUserPair()
    {
        using var context = CreateInMemoryDbContext();

        var index = context.Model.FindEntityType(typeof(Friendship))!
            .GetIndexes()
            .Single(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Friendship.PairUserLowId), nameof(Friendship.PairUserHighId)]));

        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789012345678901")]
    public void FriendshipRequestDto_RejectsInvalidPhoneNumberLength(string phoneNumber)
    {
        var request = new FriendshipRequestDto { PhoneNumber = phoneNumber };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
    }

    private static Friendship CreateFriendship(User requester, User addressee, FriendshipStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RequesterId = requester.Id,
        AddresseeId = addressee.Id,
        PairUserLowId = GetPairLowId(requester.Id, addressee.Id),
        PairUserHighId = GetPairHighId(requester.Id, addressee.Id),
        Requester = requester,
        Addressee = addressee,
        Status = status
    };

    private static Guid GetPairLowId(Guid first, Guid second) =>
        string.CompareOrdinal(first.ToString("N"), second.ToString("N")) < 0 ? first : second;

    private static Guid GetPairHighId(Guid first, Guid second) =>
        string.CompareOrdinal(first.ToString("N"), second.ToString("N")) < 0 ? second : first;
}
