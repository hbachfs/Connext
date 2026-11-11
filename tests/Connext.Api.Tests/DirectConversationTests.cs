using Connext.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Connext.Api.Tests;

public sealed class DirectConversationTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    public DirectConversationTests()
    {
        _conn.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
        db.Users.Add(new User { Id = _a, FullName = "A", Phone = "0900000011", Email = "a@x.com", PasswordHash = "x" });
        db.Users.Add(new User { Id = _b, FullName = "B", Phone = "0900000012", Email = "b@x.com", PasswordHash = "x" });
        db.SaveChanges();
    }

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

    [Fact]
    public async Task Same_pair_in_any_order_gives_one_conversation()
    {
        using var db1 = NewDb();
        var c1 = await DirectConversations.GetOrCreateAsync(db1, _a, _b);
        using var db2 = NewDb();
        var c2 = await DirectConversations.GetOrCreateAsync(db2, _b, _a);

        Assert.Equal(c1, c2);
        using var db = NewDb();
        Assert.Equal(1, db.Conversations.Count());
        Assert.Equal(2, db.ConversationMembers.Count());
    }

    [Fact]
    public async Task Reopening_unhides_only_for_the_caller_and_keeps_the_history_cutoff()
    {
        using (var db = NewDb()) await DirectConversations.GetOrCreateAsync(db, _a, _b);

        var cutoff = new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
        using (var db = NewDb())
        {
            await db.ConversationMembers.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsHidden, true)
                .SetProperty(m => m.ClearedAt, (DateTime?)cutoff));
        }

        using (var db = NewDb()) await DirectConversations.GetOrCreateAsync(db, _a, _b);

        using var check = NewDb();
        var mine = check.ConversationMembers.Single(m => m.UserId == _a);
        var theirs = check.ConversationMembers.Single(m => m.UserId == _b);
        Assert.False(mine.IsHidden);
        Assert.True(theirs.IsHidden);
        Assert.Equal(cutoff, mine.ClearedAt);   // lịch sử đã xóa không hiện lại
    }

    public void Dispose() => _conn.Dispose();
}
