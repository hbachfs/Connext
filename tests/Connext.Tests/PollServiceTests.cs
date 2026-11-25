using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class PollServiceTests
{
    private static readonly string[] TwoOptions = ["Có", "Không"];

    private static (AppDbContext db, PollService svc) Create()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        return (db, new PollService(db));
    }

    private static async Task<(Guid conv, Guid a, Guid b, Guid outsider)> Seed(AppDbContext db, bool isGroup = true)
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), outsider = Guid.NewGuid();
        var conv = new Conversation { IsGroup = isGroup, Name = "Nhóm test", CreatedById = a };
        foreach (var u in new[] { a, b, c })
            conv.Members.Add(new ConversationMember { UserId = u, Role = u == a ? "Admin" : "Member" });
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();
        return (conv.Id, a, b, outsider);
    }

    [Fact]
    public async Task Create_Valid_ReturnsPollAndPollMessage()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        var r = await svc.CreateAsync(a, conv, "  Ăn gì?  ", ["Phở", "Bún", " "]);
        Assert.False(r.Ok && r.Poll!.Options.Count != 2);   // ô trống bị bỏ
        Assert.True(r.Ok);
        Assert.Equal("Ăn gì?", r.Poll!.Question);
        Assert.Equal("poll", r.Message!.Type);
        Assert.Equal(1, await db.Messages.CountAsync());
    }

    [Fact]
    public async Task Create_Outsider_Rejected()
    {
        var (db, svc) = Create(); var (conv, _, _, outsider) = await Seed(db);
        var r = await svc.CreateAsync(outsider, conv, "Q?", TwoOptions);
        Assert.Equal(PollError.NotMember, r.Error);
        Assert.Equal(0, await db.Polls.CountAsync());
    }

    [Fact]
    public async Task Create_LessThanTwoOptions_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        Assert.Equal(PollError.InvalidOptions, (await svc.CreateAsync(a, conv, "Q?", ["Chỉ một"])).Error);
        Assert.Equal(PollError.InvalidOptions, (await svc.CreateAsync(a, conv, "Q?", ["A", "  "])).Error);
    }

    [Fact]
    public async Task Create_MoreThanTenOptions_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        var eleven = Enumerable.Range(1, 11).Select(i => $"Lựa chọn {i}");
        Assert.Equal(PollError.InvalidOptions, (await svc.CreateAsync(a, conv, "Q?", eleven)).Error);
        var ten = Enumerable.Range(1, 10).Select(i => $"Lựa chọn {i}");
        Assert.True((await svc.CreateAsync(a, conv, "Q?", ten)).Ok);
    }

    [Fact]
    public async Task Create_QuestionEmptyOrTooLong_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        Assert.Equal(PollError.InvalidQuestion, (await svc.CreateAsync(a, conv, "  ", TwoOptions)).Error);
        Assert.Equal(PollError.InvalidQuestion, (await svc.CreateAsync(a, conv, new string('x', 201), TwoOptions)).Error);
        Assert.True((await svc.CreateAsync(a, conv, new string('x', 200), TwoOptions)).Ok);
    }

    [Fact]
    public async Task Create_InDirectConversation_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db, isGroup: false);
        Assert.Equal(PollError.NotGroup, (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Error);
    }

    [Fact]
    public async Task Vote_TwoMembers_CountsAndPercent()
    {
        var (db, svc) = Create(); var (conv, a, b, _) = await Seed(db);
        var poll = (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Poll!;
        await svc.VoteAsync(a, conv, poll.PollId, poll.Options[0].Id);
        var r = await svc.VoteAsync(b, conv, poll.PollId, poll.Options[1].Id);

        Assert.True(r.Ok);
        Assert.Equal(2, r.Poll!.TotalVotes);
        Assert.All(r.Poll.Options, o => { Assert.Equal(1, o.VoteCount); Assert.Equal(50, o.Percent); });
        Assert.Equal(poll.Options[1].Id, r.Poll.MyOptionId);
    }

    [Fact]
    public async Task Vote_ChangeVote_MovesTheSingleVote()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        var poll = (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Poll!;
        await svc.VoteAsync(a, conv, poll.PollId, poll.Options[0].Id);
        var r = await svc.VoteAsync(a, conv, poll.PollId, poll.Options[1].Id);

        Assert.Equal(1, r.Poll!.TotalVotes);
        Assert.Equal(0, r.Poll.Options[0].VoteCount);
        Assert.Equal(100, r.Poll.Options[1].Percent);
        Assert.Equal(1, await db.PollVotes.CountAsync());
    }

    [Fact]
    public async Task Vote_Outsider_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, outsider) = await Seed(db);
        var poll = (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Poll!;
        var r = await svc.VoteAsync(outsider, conv, poll.PollId, poll.Options[0].Id);
        Assert.Equal(PollError.NotMember, r.Error);
        Assert.Equal(0, await db.PollVotes.CountAsync());
    }

    [Fact]
    public async Task Vote_UnknownOption_Rejected()
    {
        var (db, svc) = Create(); var (conv, a, _, _) = await Seed(db);
        var poll = (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Poll!;
        Assert.Equal(PollError.InvalidOption, (await svc.VoteAsync(a, conv, poll.PollId, Guid.NewGuid())).Error);
    }

    [Fact]
    public async Task Get_ReturnsResultsAndMyOption()
    {
        var (db, svc) = Create(); var (conv, a, b, _) = await Seed(db);
        var poll = (await svc.CreateAsync(a, conv, "Q?", TwoOptions)).Poll!;
        await svc.VoteAsync(b, conv, poll.PollId, poll.Options[0].Id);

        var mine = await svc.GetAsync(b, conv, poll.PollId);
        var other = await svc.GetAsync(a, conv, poll.PollId);
        Assert.Equal(poll.Options[0].Id, mine.Poll!.MyOptionId);
        Assert.Null(other.Poll!.MyOptionId);
        Assert.Equal(1, other.Poll.TotalVotes);
    }
}