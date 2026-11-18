
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class DirectConversationTests
{
    private static (AppDbContext db, DirectConversationService svc) Setup()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        return (db, new DirectConversationService(db));
    }

    // Tạo 2 user. Nếu User có trường bắt buộc (Email, Phone...) thì gán thêm ở đây.
    private static async Task<(Guid a, Guid b)> SeedUsersAsync(AppDbContext db, bool friends)
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        db.Users.AddRange(new User { Id = a }, new User { Id = b });
        if (friends)
            db.Friendships.Add(new Friendship
            {
                RequesterId = a,
                AddresseeId = b,
                PairUserLowId = a.CompareTo(b) < 0 ? a : b,
                PairUserHighId = a.CompareTo(b) < 0 ? b : a,
                Status = FriendshipStatus.Accepted
            });
        await db.SaveChangesAsync();
        return (a, b);
    }

    [Fact]
    public async Task Create_New_When_Friends_And_None_Exists()
    {
        var (db, svc) = Setup();
        var (a, b) = await SeedUsersAsync(db, friends: true);

        var r = await svc.GetOrCreateForFriendAsync(a, b);

        Assert.True(r.Ok);
        Assert.True(r.Created);
        Assert.False(r.Conversation!.IsGroup);
        Assert.Equal(2, r.Conversation.Members.Count);
    }

    [Fact]
    public async Task Call_Again_Does_Not_Create_Duplicate_Both_Directions()
    {
        var (db, svc) = Setup();
        var (a, b) = await SeedUsersAsync(db, friends: true);

        var first = await svc.GetOrCreateForFriendAsync(a, b);
        var second = await svc.GetOrCreateForFriendAsync(a, b);
        var reverse = await svc.GetOrCreateForFriendAsync(b, a);

        Assert.Equal(first.Conversation!.Id, second.Conversation!.Id);
        Assert.Equal(first.Conversation.Id, reverse.Conversation!.Id);
        Assert.False(second.Created);
        Assert.Equal(1, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task Stranger_Is_Rejected()
    {
        var (db, svc) = Setup();
        var (a, b) = await SeedUsersAsync(db, friends: false);

        var r = await svc.GetOrCreateForFriendAsync(a, b);

        Assert.Equal(DirectError.NotFriends, r.Error);
        Assert.Equal(0, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task Pending_Request_Is_Not_Friend()
    {
        var (db, svc) = Setup();
        var (a, b) = await SeedUsersAsync(db, friends: false);
        db.Friendships.Add(new Friendship { RequesterId = a, AddresseeId = b, Status = FriendshipStatus.Pending });
        await db.SaveChangesAsync();

        var r = await svc.GetOrCreateForFriendAsync(a, b);

        Assert.Equal(DirectError.NotFriends, r.Error);
    }

    [Fact]
    public async Task Ensure_After_Accept_Does_Not_Duplicate()
    {
        var (db, svc) = Setup();
        var (a, b) = await SeedUsersAsync(db, friends: true);

        var (c1, created1) = await svc.EnsureAsync(a, b);
        var (c2, created2) = await svc.EnsureAsync(b, a);

        Assert.True(created1);
        Assert.False(created2);
        Assert.Equal(c1.Id, c2.Id);
    }

    // TODO: test "người bị chặn bị từ chối" sau khi có bảng chặn và IsBlockedAsync thật.
}