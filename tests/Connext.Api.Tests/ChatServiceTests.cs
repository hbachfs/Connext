using Connext.Shared.Dtos;

using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Connext.Tests;

public class ChatServiceTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Chỉnh lại object khởi tạo User nếu BE2 có thêm trường bắt buộc
    private static User NewUser(AppDbContext db)
    {
var id = Guid.NewGuid();
        var u = new User
        {
            Id = id,
            FullName = $"User {id}",
            Phone = $"09{Random.Shared.Next(10000000, 99999999)}",
            Email = $"{id:N}@connext.test",
            PasswordHash = "test_hash"
        };
        db.Users.Add(u);
        return u;
    }

    private static void Befriend(AppDbContext db, User a, User b, string st = FriendshipStatus.Accepted) =>
        db.Friendships.Add(new Friendship { RequesterId = a.Id, AddresseeId = b.Id, Status = st });

    private static async Task<(AppDbContext db, ChatService svc, User a, User b)> SetupFriendsAsync()
    {
        var db = NewDb();
        var a = NewUser(db); var b = NewUser(db);
        Befriend(db, a, b);
        await db.SaveChangesAsync();
        return (db, new ChatService(db), a, b);
    }

    [Fact]
    public async Task Send_Friends_SavesMessageAndCreatesConversation()
    {
        var (db, svc, a, b) = await SetupFriendsAsync();
        var dto = await svc.SendAsync(a.Id, b.Id, "hello");

        Assert.NotNull(dto);
        Assert.Equal("hello", dto!.Content);
        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(2, await db.ConversationMembers.CountAsync());
    }

    [Fact]
    public async Task Send_NotFriends_ReturnsNull()
    {
        var db = NewDb();
        var a = NewUser(db); var b = NewUser(db);
        await db.SaveChangesAsync();

        Assert.Null(await new ChatService(db).SendAsync(a.Id, b.Id, "hi"));
    }

    [Fact]
    public async Task Send_PendingFriendship_ReturnsNull()
    {
        var db = NewDb();
        var a = NewUser(db); var b = NewUser(db);
        Befriend(db, a, b, FriendshipStatus.Pending);
        await db.SaveChangesAsync();

        Assert.Null(await new ChatService(db).SendAsync(a.Id, b.Id, "hi"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Send_EmptyContent_ReturnsNull(string content)
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        Assert.Null(await svc.SendAsync(a.Id, b.Id, content));
    }

    [Fact]
    public async Task Send_ToSelf_ReturnsNull()
    {
        var (_, svc, a, _) = await SetupFriendsAsync();
        Assert.Null(await svc.SendAsync(a.Id, a.Id, "hi"));
    }

    [Fact]
    public async Task Send_TwoDirections_ReusesSameConversation()
    {
        var (db, svc, a, b) = await SetupFriendsAsync();
        await svc.SendAsync(a.Id, b.Id, "1");
        await svc.SendAsync(b.Id, a.Id, "2");

        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(2, await db.Messages.CountAsync());
    }

    [Fact]
    public async Task GetMessages_FirstPage_Returns10Newest_WithCursor()
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        MessageDto? last = null;
        for (var i = 1; i <= 25; i++) last = await svc.SendAsync(a.Id, b.Id, $"m{i}");

        var page = await svc.GetMessagesAsync(last!.ConversationId, a.Id, null);

        Assert.NotNull(page);
        Assert.Equal(10, page!.Items.Count);
        Assert.Equal("m25", page.Items[0].Content);
        Assert.Equal("m16", page.Items[^1].Content);
        Assert.Equal(page.Items[^1].Id, page.NextBeforeId);
    }

    [Fact]
    public async Task GetMessages_UsesBeforeId_AndEndsWithNullCursor()
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        MessageDto? last = null;
        for (var i = 1; i <= 25; i++) last = await svc.SendAsync(a.Id, b.Id, $"m{i}");
        var convId = last!.ConversationId;

        var p1 = await svc.GetMessagesAsync(convId, a.Id, null);
        var p2 = await svc.GetMessagesAsync(convId, a.Id, p1!.NextBeforeId);
        var p3 = await svc.GetMessagesAsync(convId, a.Id, p2!.NextBeforeId);

        Assert.Equal("m15", p2.Items[0].Content);
        Assert.Equal(5, p3!.Items.Count);
        Assert.Null(p3.NextBeforeId);
    }

    [Fact]
    public async Task GetMessages_Exactly10_HasNoNextCursor()
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        MessageDto? last = null;
        for (var i = 1; i <= 10; i++) last = await svc.SendAsync(a.Id, b.Id, $"m{i}");

        var page = await svc.GetMessagesAsync(last!.ConversationId, a.Id, null);
        Assert.Null(page!.NextBeforeId);
    }

    [Fact]
    public async Task GetMessages_NonMember_ReturnsNull()
    {
        var (db, svc, a, b) = await SetupFriendsAsync();
        var dto = await svc.SendAsync(a.Id, b.Id, "secret");
        var stranger = NewUser(db);
        await db.SaveChangesAsync();

        Assert.Null(await svc.GetMessagesAsync(dto!.ConversationId, stranger.Id, null));
    }

    [Fact]
    public async Task MarkRead_MarksOnlyOtherUsersMessages_UpToId()
    {
        var (db, svc, a, b) = await SetupFriendsAsync();
        var m1 = await svc.SendAsync(a.Id, b.Id, "1");
        var m2 = await svc.SendAsync(a.Id, b.Id, "2");
        var m3 = await svc.SendAsync(a.Id, b.Id, "3");
        var own = await svc.SendAsync(b.Id, a.Id, "reply");

        var updated = await svc.MarkReadAsync(m1!.ConversationId, b.Id, m2!.Id);

        Assert.Equal(2, updated);
        Assert.NotNull((await db.Messages.FindAsync(m1.Id))!.ReadAt);
        Assert.NotNull((await db.Messages.FindAsync(m2.Id))!.ReadAt);
        Assert.Null((await db.Messages.FindAsync(m3!.Id))!.ReadAt);   // vượt upToId
        Assert.Null((await db.Messages.FindAsync(own!.Id))!.ReadAt);  // tin của chính mình
    }

    [Fact]
    public async Task MarkRead_NonMember_ReturnsNull()
    {
        var (db, svc, a, b) = await SetupFriendsAsync();
        var dto = await svc.SendAsync(a.Id, b.Id, "x");
        var stranger = NewUser(db);
        await db.SaveChangesAsync();

        Assert.Null(await svc.MarkReadAsync(dto!.ConversationId, stranger.Id, dto.Id));
    }

    [Fact]
    public async Task MarkRead_CalledTwice_IsIdempotent()
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        var dto = await svc.SendAsync(a.Id, b.Id, "x");

        Assert.Equal(1, await svc.MarkReadAsync(dto!.ConversationId, b.Id, dto.Id));
        Assert.Equal(0, await svc.MarkReadAsync(dto.ConversationId, b.Id, dto.Id));
    }

    [Fact]
    public async Task GetOtherMember_ReturnsPeer()
    {
        var (_, svc, a, b) = await SetupFriendsAsync();
        var dto = await svc.SendAsync(a.Id, b.Id, "x");

        Assert.Equal(b.Id, await svc.GetOtherMemberAsync(dto!.ConversationId, a.Id));
    }
}
