using System.ComponentModel.DataAnnotations;
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Contacts;
using Connext.Shared;
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
        PhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber),
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
    public async Task DeclinedRequest_CanBeSentAgain()
    {
        using var context = CreateInMemoryDbContext();
        var requester = CreateUser("0900000001", "Requester");
        var addressee = CreateUser("0900000002", "Addressee");
        context.Users.AddRange(requester, addressee);
        await context.SaveChangesAsync();
        var service = new ContactService(context);

        Assert.True(await service.SendFriendRequestAsync(requester.Id, addressee.PhoneNumber));
        var request = await context.Friendships.SingleAsync();
        Assert.True(await service.RespondToRequestAsync(addressee.Id, request.Id, accept: false));
        Assert.True(await service.SendFriendRequestAsync(requester.Id, addressee.PhoneNumber));

        Assert.Equal(FriendshipStatus.Pending, request.Status);
        Assert.Equal(requester.Id, request.RequesterId);
    }

    [Fact]
    public async Task GetRequestsAsync_ReturnsReceivedAndSentRequests()
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateUser("0900000001", "Current");
        var incoming = CreateUser("0900000002", "Incoming");
        var outgoing = CreateUser("0900000003", "Outgoing");
        context.Users.AddRange(user, incoming, outgoing);
        context.Friendships.AddRange(
            CreateFriendship(incoming, user, FriendshipStatus.Pending),
            CreateFriendship(user, outgoing, FriendshipStatus.Pending));
        await context.SaveChangesAsync();

        var requests = await new ContactService(context).GetRequestsAsync(user.Id);

        Assert.Equal(incoming.Id, Assert.Single(requests.Received).UserId);
        Assert.Equal(outgoing.Id, Assert.Single(requests.Sent).UserId);
    }

    [Fact]
    public async Task BlockUser_RemovesFriendshipAndHidesBlockedUser()
    {
        using var context = CreateInMemoryDbContext();
        var blocker = CreateUser("0900000001", "Blocker");
        var blocked = CreateUser("0900000002", "Blocked");
        context.Users.AddRange(blocker, blocked);
        context.Friendships.Add(CreateFriendship(blocker, blocked, FriendshipStatus.Accepted));
        await context.SaveChangesAsync();
        var service = new ContactService(context);

        Assert.True(await service.BlockUserAsync(blocker.Id, blocked.Id));
        Assert.Empty(await service.SearchUsersAsync(blocked.Id, "Blocker"));
        Assert.Empty(await service.GetFriendsAsync(blocker.Id));
        Assert.False(await service.SendFriendRequestAsync(blocked.Id, blocker.PhoneNumber));
        Assert.True(await service.UnblockUserAsync(blocker.Id, blocked.Id));
        Assert.Single(await service.SearchUsersAsync(blocked.Id, "Blocker"));
    }

    [Fact]
    public async Task RemoveFriendAsync_DeletesOnlyAcceptedFriendship()
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateUser("0900000001", "User");
        var friend = CreateUser("0900000002", "Friend");
        context.Users.AddRange(user, friend);
        context.Friendships.Add(CreateFriendship(user, friend, FriendshipStatus.Accepted));
        await context.SaveChangesAsync();

        Assert.True(await new ContactService(context).RemoveFriendAsync(user.Id, friend.Id));
        Assert.Empty(await context.Friendships.ToListAsync());
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
