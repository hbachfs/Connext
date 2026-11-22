
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class CallManagerTests
{
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeNotifier : ICallNotifier
    {
        public HashSet<Guid> Online { get; } = new();
        public List<(Guid[] Users, string Method, object Payload)> Sent { get; } = new();
        public bool IsOnline(Guid userId) => Online.Contains(userId);
        public Task SendAsync(IEnumerable<Guid> userIds, string method, object payload)
        {
            Sent.Add((userIds.ToArray(), method, payload));
            return Task.CompletedTask;
        }
        public bool Got(string method, Guid user) => Sent.Any(s => s.Method == method && s.Users.Contains(user));
    }

    private sealed class Env
    {
        public required CallManager Calls { get; init; }
        public required FakeNotifier Notifier { get; init; }
        public required TestClock Clock { get; init; }
        public required ServiceProvider Services { get; init; }
        public required Guid A { get; init; }   // người gọi
        public required Guid B { get; init; }   // người nhận

        public async Task<List<Message>> MessagesAsync()
        {
            using var s = Services.CreateScope();
            return await s.ServiceProvider.GetRequiredService<AppDbContext>().Messages.ToListAsync();
        }
    }

    private static async Task<Env> SetupAsync(bool friends = true, bool bOnline = true)
    {
        var dbName = Guid.NewGuid().ToString();
        var sc = new ServiceCollection();
        sc.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        sc.AddScoped<IDirectConversationService, DirectConversationService>();
        var sp = sc.BuildServiceProvider();

        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(TestData.NewUser(a), TestData.NewUser(b));
            if (friends)
                db.Friendships.Add(new Friendship
                {
                    RequesterId = a,
                    AddresseeId = b,
                    Status = FriendshipStatus.Accepted,
                    PairUserLowId = a.CompareTo(b) < 0 ? a : b,
                    PairUserHighId = a.CompareTo(b) < 0 ? b : a
                });
            await db.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        if (bOnline) notifier.Online.Add(b);
        var clock = new TestClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var calls = new CallManager(sp.GetRequiredService<IServiceScopeFactory>(), notifier, clock,
            Options.Create(new CallOptions { RingSeconds = 600 }), NullLogger<CallManager>.Instance);

        return new Env { Calls = calls, Notifier = notifier, Clock = clock, Services = sp, A = a, B = b };
    }

    // ---------- Quyền ----------
    [Fact]
    public async Task Friend_Can_Call_And_Callee_Gets_IncomingCall()
    {
        var e = await SetupAsync();
        var r = await e.Calls.StartAsync(e.A, e.B, video: true);

        Assert.True(r.Ok);
        Assert.True(e.Notifier.Got("IncomingCall", e.B));
        Assert.True(e.Calls.IsInCall(e.A) && e.Calls.IsInCall(e.B));
    }

    [Fact]
    public async Task Stranger_Cannot_Call()
    {
        var e = await SetupAsync(friends: false);
        Assert.Equal(CallError.NotFriends, (await e.Calls.StartAsync(e.A, e.B, false)).Error);
        Assert.False(e.Calls.IsInCall(e.A));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Blocked_Cannot_Call_Both_Directions(bool callerBlocks)
    {
        var e = await SetupAsync();
        using (var s = e.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserBlocks.Add(callerBlocks
                ? new UserBlock { BlockerId = e.A, BlockedUserId = e.B }
                : new UserBlock { BlockerId = e.B, BlockedUserId = e.A });
            await db.SaveChangesAsync();
        }

        Assert.Equal(CallError.Blocked, (await e.Calls.StartAsync(e.A, e.B, false)).Error);
    }

    [Fact]
    public async Task Cannot_Call_Self()
    {
        var e = await SetupAsync();
        Assert.Equal(CallError.InvalidTarget, (await e.Calls.StartAsync(e.A, e.A, false)).Error);
    }

    // ---------- Máy bận, offline ----------
    [Fact]
    public async Task Busy_Callee_Gets_Busy_And_Missed_Call_Logged()
    {
        var e = await SetupAsync();
        var c = Guid.NewGuid();
        using (var s = e.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(TestData.NewUser(c));
            db.Friendships.Add(new Friendship { RequesterId = c, AddresseeId = e.B, Status = FriendshipStatus.Accepted });
            await db.SaveChangesAsync();
        }
        await e.Calls.StartAsync(e.A, e.B, false);   // B đang đổ chuông / trong cuộc gọi

        var r = await e.Calls.StartAsync(c, e.B, false);

        Assert.Equal(CallError.Busy, r.Error);
        Assert.Contains(await e.MessagesAsync(), m => m.Content!.Contains("nhỡ"));
    }

    [Fact]
    public async Task Caller_Already_In_Call_Is_Busy()
    {
        var e = await SetupAsync();
        await e.Calls.StartAsync(e.A, e.B, false);

        Assert.Equal(CallError.Busy, (await e.Calls.StartAsync(e.A, e.B, false)).Error);
    }

    [Fact]
    public async Task Offline_Callee_Logs_Missed_Call()
    {
        var e = await SetupAsync(bOnline: false);

        var r = await e.Calls.StartAsync(e.A, e.B, false);

        Assert.Equal(CallError.Offline, r.Error);
        var msg = Assert.Single(await e.MessagesAsync());
        Assert.Equal("system", msg.Type);
        Assert.Equal("Cuộc gọi thoại nhỡ", msg.Content);
        Assert.False(e.Calls.IsInCall(e.A));
    }

    // ---------- Chấp nhận / từ chối / hủy ----------
    [Fact]
    public async Task Callee_Accepts_Then_Call_Is_Active()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;

        Assert.True((await e.Calls.AcceptAsync(e.B, id)).Ok);

        Assert.True(e.Notifier.Got("CallAccepted", e.A));
        Assert.Equal(CallError.InvalidState, (await e.Calls.AcceptAsync(e.B, id)).Error);   // chấp nhận lần hai
    }

    [Fact]
    public async Task Caller_Cannot_Accept_Own_Call()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;

        Assert.Equal(CallError.NotAllowed, (await e.Calls.AcceptAsync(e.A, id)).Error);
    }

    [Fact]
    public async Task Outsider_Cannot_Touch_Call()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        var stranger = Guid.NewGuid();

        Assert.Equal(CallError.NotFound, (await e.Calls.AcceptAsync(stranger, id)).Error);
        Assert.Equal(CallError.NotFound, (await e.Calls.EndAsync(stranger, id)).Error);
    }

    [Fact]
    public async Task Reject_Ends_Call_Logs_And_Frees_Both_Users()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;

        Assert.True((await e.Calls.RejectAsync(e.B, id)).Ok);

        Assert.True(e.Notifier.Got("CallEnded", e.A));
        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi thoại bị từ chối");
        Assert.False(e.Calls.IsInCall(e.A) || e.Calls.IsInCall(e.B));
        Assert.True((await e.Calls.StartAsync(e.A, e.B, false)).Ok);   // gọi lại được
    }

    [Fact]
    public async Task Caller_Cancels_While_Ringing_Logs_Missed()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, true)).CallId;

        Assert.True((await e.Calls.EndAsync(e.A, id)).Ok);

        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi video nhỡ");
    }

    [Fact]
    public async Task Ring_Timeout_Logs_Missed_And_Frees_Users()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;

        await e.Calls.TimeoutAsync(id);

        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi thoại nhỡ");
        Assert.True(e.Notifier.Got("CallEnded", e.B));
        Assert.False(e.Calls.IsInCall(e.A));
    }

    [Fact]
    public async Task Timeout_After_Accept_Does_Nothing()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        await e.Calls.AcceptAsync(e.B, id);

        await e.Calls.TimeoutAsync(id);

        Assert.True(e.Calls.IsInCall(e.A));
        Assert.Empty(await e.MessagesAsync());
    }

    // ---------- Thời lượng ----------
    [Fact]
    public async Task Ended_Call_Logs_Duration()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        await e.Calls.AcceptAsync(e.B, id);
        e.Clock.Now = e.Clock.Now.AddSeconds(125);

        await e.Calls.EndAsync(e.A, id);

        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi thoại · 02:05");
    }

    // ---------- SDP / ICE ----------
    [Fact]
    public async Task Relay_Requires_Active_Call()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;

        Assert.Equal(CallError.InvalidState, (await e.Calls.RelayAsync(e.A, id, "offer", "sdp")).Error);
    }

    [Fact]
    public async Task Relay_Forwards_Only_To_Peer()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        await e.Calls.AcceptAsync(e.B, id);

        Assert.True((await e.Calls.RelayAsync(e.A, id, "offer", "v=0...")).Ok);

        var sent = e.Notifier.Sent.Last(s => s.Method == "ReceiveSignal");
        Assert.Equal(new[] { e.B }, sent.Users);
    }

    [Fact]
    public async Task Relay_Rejects_Outsider_And_Oversized()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        await e.Calls.AcceptAsync(e.B, id);

        Assert.Equal(CallError.NotFound, (await e.Calls.RelayAsync(Guid.NewGuid(), id, "ice", "x")).Error);
        Assert.Equal(CallError.TooLarge, (await e.Calls.RelayAsync(e.A, id, "offer", new string('x', 20001))).Error);
    }

    // ---------- Mất kết nối ----------
    [Fact]
    public async Task Peer_Disconnect_During_Active_Call_Ends_With_ConnectionLost()
    {
        var e = await SetupAsync();
        var id = (await e.Calls.StartAsync(e.A, e.B, false)).CallId;
        await e.Calls.AcceptAsync(e.B, id);
        e.Clock.Now = e.Clock.Now.AddSeconds(30);

        await e.Calls.HandleUserOfflineAsync(e.B);

        var ended = e.Notifier.Sent.Last(s => s.Method == "CallEnded");
        Assert.Contains(e.A, ended.Users);
        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi thoại bị gián đoạn · 00:30");
        Assert.False(e.Calls.IsInCall(e.A));
    }

    [Fact]
    public async Task Caller_Disconnect_While_Ringing_Logs_Missed()
    {
        var e = await SetupAsync();
        await e.Calls.StartAsync(e.A, e.B, false);

        await e.Calls.HandleUserOfflineAsync(e.A);

        Assert.Contains(await e.MessagesAsync(), m => m.Content == "Cuộc gọi thoại nhỡ");
        Assert.False(e.Calls.IsInCall(e.B));
    }
}