using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class MessageActionTests
{
	private class FixedTime : TimeProvider
	{
		public DateTimeOffset Now { get; set; }
		public FixedTime(DateTimeOffset now) { Now = now; }
		public override DateTimeOffset GetUtcNow() => Now;
	}

	private static readonly DateTime T0 = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

	private static (AppDbContext db, MessageActionService svc, FixedTime time) Setup()
	{
		var opts = new DbContextOptionsBuilder<AppDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
		var db = new AppDbContext(opts);
		var time = new FixedTime(new DateTimeOffset(T0));
		return (db, new MessageActionService(db, time), time);
	}

	// Hội thoại có 2 thành viên (a gửi, b nhận) và 1 tin do a gửi lúc T0
	private static async Task<(Guid conv, Guid a, Guid b, long msgId)> SeedAsync(AppDbContext db, bool isGroup = false)
	{
		Guid a = Guid.NewGuid(), b = Guid.NewGuid();
		db.Users.AddRange(TestData.NewUser(a), TestData.NewUser(b));
		var conv = new Conversation { Id = Guid.NewGuid(), IsGroup = isGroup };
		conv.Members.Add(new ConversationMember { UserId = a, Role = "Admin" });
		conv.Members.Add(new ConversationMember { UserId = b, Role = "Member" });
		db.Conversations.Add(conv);
		var msg = new Message { ConversationId = conv.Id, SenderId = a, Type = "text", Content = "xin chào", Status = "Sent", CreatedAt = T0 };
		db.Messages.Add(msg);
		await db.SaveChangesAsync();
		return (conv.Id, a, b, msg.Id);
	}

	private static async Task<long> AddMessageAsync(AppDbContext db, Guid conv, Guid sender, string text)
	{
		var m = new Message { ConversationId = conv, SenderId = sender, Type = "text", Content = text, Status = "Sent", CreatedAt = T0 };
		db.Messages.Add(m);
		await db.SaveChangesAsync();
		return m.Id;
	}

	// ---------- Reaction ----------
	[Fact]
	public async Task Reaction_Add_Change_Remove()
	{
		var (db, svc, _) = Setup();
		var (_, a, b, id) = await SeedAsync(db);

		Assert.True((await svc.SetReactionAsync(b, id, "👍")).Ok);
		Assert.Equal("👍", (await db.MessageReactions.SingleAsync()).Emoji);

		Assert.True((await svc.SetReactionAsync(b, id, "❤️")).Ok);
		Assert.Equal("❤️", (await db.MessageReactions.SingleAsync()).Emoji);

		Assert.True((await svc.RemoveReactionAsync(b, id)).Ok);
		Assert.Equal(0, await db.MessageReactions.CountAsync());
	}

	[Fact]
	public async Task Reaction_Invalid_Emoji_Rejected()
	{
		var (db, svc, _) = Setup();
		var (_, _, b, id) = await SeedAsync(db);

		Assert.Equal(MsgError.InvalidEmoji, (await svc.SetReactionAsync(b, id, "abc")).Error);
	}

	[Fact]
	public async Task Reaction_By_NonMember_Rejected()
	{
		var (db, svc, _) = Setup();
		var (_, _, _, id) = await SeedAsync(db);

		Assert.Equal(MsgError.NotFound, (await svc.SetReactionAsync(Guid.NewGuid(), id, "👍")).Error);
	}

	[Fact]
	public async Task Reaction_Result_Notifies_All_Members()
	{
		var (db, svc, _) = Setup();
		var (_, a, b, id) = await SeedAsync(db);

		var r = await svc.SetReactionAsync(b, id, "😂");

		Assert.Contains(a, r.MemberIds);
		Assert.Contains(b, r.MemberIds);
	}

	// ---------- Thu hồi ----------
	[Fact]
	public async Task Recall_By_Sender_Within_Window_Succeeds_And_Clears_Content()
	{
		var (db, svc, time) = Setup();
		var (_, a, _, id) = await SeedAsync(db);
		time.Now = new DateTimeOffset(T0.AddMinutes(5));

		Assert.True((await svc.RecallAsync(a, id)).Ok);

		var msg = await db.Messages.SingleAsync();
		Assert.True(msg.IsRecalled);
		Assert.Null(msg.Content);
		Assert.Equal(MessageMapper.RecalledText, MessageMapper.ToDto(msg, true).Content);
	}

	[Fact]
	public async Task Recall_By_NonSender_Rejected()
	{
		var (db, svc, _) = Setup();
		var (_, _, b, id) = await SeedAsync(db);

		Assert.Equal(MsgError.NotSender, (await svc.RecallAsync(b, id)).Error);
	}

	[Fact]
	public async Task Recall_After_Window_Rejected()
	{
		var (db, svc, time) = Setup();
		var (_, a, _, id) = await SeedAsync(db);
		time.Now = new DateTimeOffset(T0.AddMinutes(16));

		Assert.Equal(MsgError.WindowExpired, (await svc.RecallAsync(a, id)).Error);
		Assert.False((await db.Messages.SingleAsync()).IsRecalled);
	}

	[Fact]
	public async Task Recall_Twice_Rejected()
	{
		var (db, svc, _) = Setup();
		var (_, a, _, id) = await SeedAsync(db);

		await svc.RecallAsync(a, id);

		Assert.Equal(MsgError.AlreadyRecalled, (await svc.RecallAsync(a, id)).Error);
	}

	[Fact]
	public async Task Recall_Removes_Pin_And_Reactions_And_Blocks_New_Reaction()
	{
		var (db, svc, _) = Setup();
		var (conv, a, b, id) = await SeedAsync(db);
		await svc.PinAsync(a, conv, id);
		await svc.SetReactionAsync(b, id, "👍");

		await svc.RecallAsync(a, id);

		Assert.Equal(0, await db.MessagePins.CountAsync());
		Assert.Equal(0, await db.MessageReactions.CountAsync());
		Assert.Equal(MsgError.Recalled, (await svc.SetReactionAsync(b, id, "👍")).Error);
	}

	// ---------- Reply (phần hiển thị) ----------
	[Fact]
	public async Task Reply_Preview_Is_Truncated_And_Shows_Recalled_Text()
	{
		var (db, svc, _) = Setup();
		var (conv, a, b, orig) = await SeedAsync(db);
		var original = await db.Messages.SingleAsync(m => m.Id == orig);
		original.Content = new string('x', 300);
		var reply = new Message
		{
			ConversationId = conv,
			SenderId = b,
			Type = "text",
			Content = "ok",
			Status = "Sent",
			CreatedAt = T0,
			ReplyToMessageId = orig,
			ReplyToMessage = original
		};
		db.Messages.Add(reply);
		await db.SaveChangesAsync();

		var dto = MessageMapper.ToDto(reply, false);
		Assert.Equal(orig, dto.ReplyToMessageId);
		Assert.True(dto.ReplyTo!.Preview!.Length <= 101);

		await svc.RecallAsync(a, orig);
		Assert.Equal(MessageMapper.RecalledText, MessageMapper.ToDto(reply, false).ReplyTo!.Preview);
	}

	// ---------- Ghim ----------
	[Fact]
	public async Task Pin_And_Unpin_Work()
	{
		var (db, svc, _) = Setup();
		var (conv, a, _, id) = await SeedAsync(db);

		Assert.True((await svc.PinAsync(a, conv, id)).Ok);
		Assert.Single(await svc.GetPinsAsync(a, conv) ?? []);

		Assert.True((await svc.UnpinAsync(a, conv, id)).Ok);
		Assert.Empty(await svc.GetPinsAsync(a, conv) ?? []);
	}

	[Fact]
	public async Task Pin_Is_Idempotent()
	{
		var (db, svc, _) = Setup();
		var (conv, a, _, id) = await SeedAsync(db);

		await svc.PinAsync(a, conv, id);
		Assert.True((await svc.PinAsync(a, conv, id)).Ok);

		Assert.Equal(1, await db.MessagePins.CountAsync());
	}

	[Fact]
	public async Task Pin_Over_Limit_Rejected()
	{
		var (db, svc, _) = Setup();
		var (conv, a, _, first) = await SeedAsync(db);
		await svc.PinAsync(a, conv, first);
		for (var i = 1; i < MessageActionService.MaxPins; i++)
			await svc.PinAsync(a, conv, await AddMessageAsync(db, conv, a, $"m{i}"));

		var extra = await AddMessageAsync(db, conv, a, "thừa");

		Assert.Equal(MsgError.PinLimit, (await svc.PinAsync(a, conv, extra)).Error);
	}

	[Fact]
	public async Task Pin_Message_From_Other_Conversation_Rejected()
	{
		var (db, svc, _) = Setup();
		var (conv1, a, _, _) = await SeedAsync(db);
		var (_, _, _, otherMsg) = await SeedAsync(db);

		Assert.Equal(MsgError.NotFound, (await svc.PinAsync(a, conv1, otherMsg)).Error);
	}

	[Fact]
	public async Task Pins_Are_Hidden_From_NonMember()
	{
		var (db, svc, _) = Setup();
		var (conv, _, _, _) = await SeedAsync(db);

		Assert.Null(await svc.GetPinsAsync(Guid.NewGuid(), conv));
	}

	[Fact]
	public async Task Group_Pin_Respects_Admin_Rule()
	{
		var (db, svc, _) = Setup();
		var (conv, a, b, id) = await SeedAsync(db, isGroup: true);

		var r = await svc.PinAsync(b, conv, id);

		// Giả định hiện tại: mọi thành viên được ghim. Nếu BA chốt "chỉ admin" (GroupPinAdminOnly = true) thì kỳ vọng NotAllowed.
		Assert.Equal(MessageActionService.GroupPinAdminOnly ? MsgError.NotAllowed : MsgError.None, r.Error);
	}
}