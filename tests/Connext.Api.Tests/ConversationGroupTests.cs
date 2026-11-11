using System.Security.Claims;
using Connext.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Connext.Api.Tests;

public sealed class ConversationGroupTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _friendOne = Guid.NewGuid();
    private readonly Guid _friendTwo = Guid.NewGuid();
    private readonly Guid _stranger = Guid.NewGuid();

    public ConversationGroupTests()
    {
        _connection.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();

        db.Users.AddRange(
            User(_owner, "Owner", "owner", "owner@test.local"),
            User(_friendOne, "Friend One", "friend1", "friend1@test.local"),
            User(_friendTwo, "Friend Two", "friend2", "friend2@test.local"),
            User(_stranger, "Stranger", "stranger", "stranger@test.local"));

        db.Friendships.AddRange(Friendship(_owner, _friendOne), Friendship(_owner, _friendTwo));
        db.SaveChanges();
    }

    [Fact]
    public async Task Group_with_two_members_is_rejected()
    {
        using var db = NewDb();
        var result = await Controller(db).CreateGroup(new CreateGroupRequest
        {
            Name = "Too small",
            MemberIds = new List<Guid> { _friendOne }
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Contains("ít nhất 3 thành viên", response.Message);
        Assert.Empty(await db.Conversations.ToListAsync());
    }

    [Fact]
    public async Task Group_with_three_members_is_created()
    {
        using var db = NewDb();
        var result = await Controller(db).CreateGroup(new CreateGroupRequest
        {
            Name = "Valid group",
            MemberIds = new List<Guid> { _friendOne, _friendTwo }
        });

        Assert.IsType<OkObjectResult>(result);
        var conversation = await db.Conversations.Include(c => c.Members).SingleAsync();
        Assert.True(conversation.IsGroup);
        Assert.Equal("Valid group", conversation.Name);
        Assert.Equal(3, conversation.Members.Count);
        Assert.Equal("Admin", conversation.Members.Single(m => m.UserId == _owner).Role);
    }

    [Fact]
    public async Task Group_with_non_friend_is_rejected()
    {
        using var db = NewDb();
        var result = await Controller(db).CreateGroup(new CreateGroupRequest
        {
            Name = "Invalid group",
            MemberIds = new List<Guid> { _friendOne, _stranger }
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Contains("đã là bạn bè", response.Message);
        Assert.Empty(await db.Conversations.ToListAsync());
    }

    private ConversationsController Controller(AppDbContext db)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, _owner.ToString()) }, "test");

        return new ConversationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(_connection)
        .Options);

    private static User User(Guid id, string name, string phone, string email) => new()
    {
        Id = id,
        FullName = name,
        Phone = phone,
        Email = email,
        PasswordHash = "test-hash"
    };

    private static Friendship Friendship(Guid requesterId, Guid addresseeId) => new()
    {
        RequesterId = requesterId,
        AddresseeId = addresseeId,
        Status = "Accepted"
    };

    public void Dispose() => _connection.Dispose();
}
