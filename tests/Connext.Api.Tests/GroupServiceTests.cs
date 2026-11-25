
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GroupServiceTests
{
    private static (AppDbContext db, GroupService svc) Setup()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        return (db, new GroupService(db));
    }

    // Tạo nhóm: users[0] là Admin, còn lại là Member
    private static async Task<(Guid convId, List<Guid> users)> SeedGroupAsync(AppDbContext db, int count)
    {
        var users = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        db.Users.AddRange(users.Select(TestData.NewUser)); // CHỈNH: trường bắt buộc của User
        var conv = new Conversation { Id = Guid.NewGuid(), IsGroup = true, Name = "G" };
        for (var i = 0; i < count; i++)
            conv.Members.Add(new ConversationMember
            {
                UserId = users[i],
                Role = i == 0 ? GroupRules.Admin : GroupRules.Member,
                JoinedAt = DateTime.UtcNow.AddMinutes(i)
            });
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();
        return (conv.Id, users);
    }

    private static async Task<Guid> NewFriendOfAsync(AppDbContext db, Guid owner)
    {
        var f = Guid.NewGuid();
        db.Users.Add(TestData.NewUser(f));
        db.Friendships.Add(new Friendship { RequesterId = owner, AddresseeId = f, Status = FriendshipStatus.Accepted });
        await db.SaveChangesAsync();
        return f;
    }

    [Fact]
    public async Task Add_Friend_Succeeds()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);
        var friend = await NewFriendOfAsync(db, u[0]);

        var r = await svc.AddMembersAsync(u[0], cid, [friend]);

        Assert.True(r.Ok);
        Assert.Equal(4, await db.ConversationMembers.CountAsync(m => m.ConversationId == cid));
    }

    [Fact]
    public async Task Add_NonFriend_Rejected()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);
        var stranger = Guid.NewGuid();
        db.Users.Add(TestData.NewUser(stranger));
        await db.SaveChangesAsync();

        var r = await svc.AddMembersAsync(u[0], cid, [stranger]);

        Assert.Equal(GroupError.NotFriendOrBlocked, r.Error);
    }

    [Fact]
    public async Task Add_Blocked_Friend_Rejected()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);
        var friend = await NewFriendOfAsync(db, u[0]);
        db.UserBlocks.Add(new UserBlock { BlockerId = friend, BlockedUserId = u[0] });
        await db.SaveChangesAsync();

        var r = await svc.AddMembersAsync(u[0], cid, [friend]);

        Assert.Equal(GroupError.NotFriendOrBlocked, r.Error);
    }

    [Fact]
    public async Task Remove_By_NonAdmin_Rejected()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 4);

        var r = await svc.RemoveMemberAsync(u[1], cid, u[2]);

        Assert.Equal(GroupError.NotAdmin, r.Error);
    }

    [Fact]
    public async Task Remove_By_Admin_Succeeds_When_More_Than_Min()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 4);

        var r = await svc.RemoveMemberAsync(u[0], cid, u[2]);

        Assert.True(r.Ok);
        Assert.Contains(u[2], r.Notify); // người bị xóa vẫn nhận event
    }

    [Fact]
    public async Task Remove_Rejected_When_Group_Has_Min_Members()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);

        var r = await svc.RemoveMemberAsync(u[0], cid, u[1]);

        Assert.Equal(GroupError.MinMembers, r.Error);
    }

    [Fact]
    public async Task Leave_Disbands_Group_When_It_Drops_Below_Min_Members()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);

        var r = await svc.LeaveAsync(u[1], cid);

        Assert.True(r.Ok);
        Assert.Equal(3, r.Notify.Count);   // mọi thành viên đều nhận sự kiện
        Assert.Equal(0, await db.ConversationMembers.CountAsync(m => m.ConversationId == cid));
    }

    [Fact]
    public async Task LastAdmin_Leaves_Transfers_Admin_To_Earliest_Member()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 4); // u[0] admin, u[1] vào sớm nhất

        var r = await svc.LeaveAsync(u[0], cid);

        Assert.True(r.Ok);
        var heir = await db.ConversationMembers.SingleAsync(m => m.ConversationId == cid && m.UserId == u[1]);
        Assert.Equal(GroupRules.Admin, heir.Role);
        Assert.Equal(3, await db.ConversationMembers.CountAsync(m => m.ConversationId == cid));
    }

    [Fact]
    public async Task Cannot_Demote_Last_Admin()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 4);

        var r = await svc.SetRoleAsync(u[0], cid, u[0], GroupRules.Member);

        Assert.Equal(GroupError.LastAdmin, r.Error);
    }

    [Fact]
    public async Task Admin_Can_Promote_Member()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 4);

        var r = await svc.SetRoleAsync(u[0], cid, u[1], GroupRules.Admin);

        Assert.True(r.Ok);
    }

    [Fact]
    public async Task Rename_Updates_Name_And_Rejects_Empty()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);

        Assert.True((await svc.UpdateInfoAsync(u[0], cid, "Tên mới", null)).Ok);          // SỬA: u[0] là Admin
        Assert.Equal("Tên mới", (await db.Conversations.FindAsync(cid))!.Name);
        Assert.Equal(GroupError.Invalid, (await svc.UpdateInfoAsync(u[0], cid, "   ", null)).Error); // SỬA
    }

    [Fact]
    public async Task Avatar_Must_Be_From_Uploads()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);

        Assert.Equal(GroupError.Invalid, (await svc.UpdateInfoAsync(u[0], cid, null, "http://evil.com/a.png")).Error); // SỬA
        Assert.True((await svc.UpdateInfoAsync(u[0], cid, null, "/uploads/a.png")).Ok);                                // SỬA
    }
    [Fact]
    public async Task Rename_By_Member_Rejected()
    {
        var (db, svc) = Setup();
        var (cid, u) = await SeedGroupAsync(db, 3);

        Assert.Equal(GroupError.NotAdmin, (await svc.UpdateInfoAsync(u[1], cid, "X", null)).Error);
    }
}