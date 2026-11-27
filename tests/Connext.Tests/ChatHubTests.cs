using System.Security.Claims;
using Connext.Api.Hubs;
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Connext.Tests;

public class ChatHubTests
{
    [Fact]
    public async Task SendSignal_RejectsArbitraryEventsAndRelaysOnlyAllowedSignalsToMembers()
    {
        using var context = CreateDbContext();
        var sender = CreateUser();
        var recipient = CreateUser();
        var outsider = CreateUser();
        var conversationId = Guid.NewGuid();
        context.Users.AddRange(sender, recipient, outsider);
        context.Friendships.Add(CreateFriendship(sender, recipient));
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = sender.Id });
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversationId, UserId = sender.Id },
            new ConversationMember { ConversationId = conversationId, UserId = recipient.Id });
        await context.SaveChangesAsync();
        var callSessions = new CallSessionTracker();
        var hub = CreateHub(context, sender.Id, out var clients, callSessions);
        Assert.True(callSessions.TryStart(conversationId, sender.Id, recipient.Id, DateTime.UtcNow));

        await Assert.ThrowsAsync<HubException>(() =>
            hub.SendSignal(conversationId, recipient.Id, "ReceiveMessage", new { content = "forged" }));
        await Assert.ThrowsAsync<HubException>(() =>
            hub.SendSignal(conversationId, outsider.Id, "offer", new { sdp = "offer" }));
        Assert.Empty(clients.Messages);

        await hub.SendSignal(conversationId, recipient.Id, "offer", new { sdp = "offer" });

        var sent = Assert.Single(clients.Messages);
        Assert.Equal("offer", sent.Method);
        Assert.Equal(ChatHub.UserRoom(recipient.Id), sent.Destination);
    }

    [Fact]
    public async Task CallRelayMethods_RejectBlockedPeersAndNonMembers()
    {
        using var context = CreateDbContext();
        var sender = CreateUser();
        var recipient = CreateUser();
        var outsider = CreateUser();
        var conversationId = Guid.NewGuid();
        context.Users.AddRange(sender, recipient, outsider);
        context.Friendships.Add(CreateFriendship(sender, recipient));
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = sender.Id });
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversationId, UserId = sender.Id },
            new ConversationMember { ConversationId = conversationId, UserId = recipient.Id });
        await context.SaveChangesAsync();
        var callSessions = new CallSessionTracker();
        var hub = CreateHub(context, sender.Id, out var clients, callSessions);

        await Assert.ThrowsAsync<HubException>(() => hub.InviteCall(conversationId, outsider.Id));
        await Assert.ThrowsAsync<HubException>(() => hub.AnswerCall(conversationId, outsider.Id, true));
        await Assert.ThrowsAsync<HubException>(() =>
            hub.SendIceCandidate(conversationId, outsider.Id, new { candidate = "candidate" }));
        await Assert.ThrowsAsync<HubException>(() => hub.EndCall(conversationId, outsider.Id));

        context.UserBlocks.Add(new UserBlock { BlockerId = recipient.Id, BlockedUserId = sender.Id });
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.InviteCall(conversationId, recipient.Id));
        await Assert.ThrowsAsync<HubException>(() => hub.AnswerCall(conversationId, recipient.Id, true));
        await Assert.ThrowsAsync<HubException>(() =>
            hub.SendIceCandidate(conversationId, recipient.Id, new { candidate = "candidate" }));
        await Assert.ThrowsAsync<HubException>(() =>
            hub.SendSignal(conversationId, recipient.Id, "answer", new { sdp = "answer" }));
        await Assert.ThrowsAsync<HubException>(() => hub.EndCall(conversationId, recipient.Id));

        Assert.Empty(clients.Messages);
    }

    [Fact]
    public async Task CallFlow_HandlesAnswerSignalsAndPersistsHistory()
    {
        using var context = CreateDbContext();
        var caller = CreateUser();
        var recipient = CreateUser();
        var conversationId = Guid.NewGuid();
        context.Users.AddRange(caller, recipient);
        context.Friendships.Add(CreateFriendship(caller, recipient));
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = caller.Id });
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversationId, UserId = caller.Id },
            new ConversationMember { ConversationId = conversationId, UserId = recipient.Id });
        await context.SaveChangesAsync();

        var callSessions = new CallSessionTracker();
        var connections = new ConnectionTracker();
        connections.Add(recipient.Id, "recipient-connection");
        var callerHub = CreateHub(context, caller.Id, out var callerClients, callSessions, connections);
        var recipientHub = CreateHub(context, recipient.Id, out var recipientClients, callSessions, connections);

        await callerHub.InviteCall(conversationId, recipient.Id);
        await recipientHub.AnswerCall(conversationId, caller.Id, accepted: true);
        await callerHub.SendSignal(conversationId, recipient.Id, "offer", new { sdp = "offer" });
        await recipientHub.SendSignal(conversationId, caller.Id, "answer", new { sdp = "answer" });
        await callerHub.EndCall(conversationId, recipient.Id);

        Assert.Contains(recipientClients.Messages, message => message.Method == "CallAnswered");
        Assert.Contains(callerClients.Messages, message => message.Method == "CallIncoming");
        var callHistory = await context.Messages.SingleAsync();
        Assert.Equal(MessageType.Call, callHistory.Type);
        Assert.StartsWith("Cuộc gọi đã kết thúc", callHistory.Content);
    }

    [Fact]
    public async Task GetIceServers_RequiresMembershipAndReturnsConfiguredServers()
    {
        using var context = CreateDbContext();
        var member = CreateUser();
        var outsider = CreateUser();
        var stranger = CreateUser();
        var conversationId = Guid.NewGuid();
        context.Users.AddRange(member, outsider, stranger);
        context.Friendships.Add(CreateFriendship(member, outsider));
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = member.Id });
        context.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversationId,
            UserId = member.Id
        });
        context.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversationId,
            UserId = outsider.Id
        });
        await context.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls:0"] = "stun:stun.example.test:3478",
            ["WebRtc:IceServers:1:Urls:0"] = "turn:turn.example.test:3478",
            ["WebRtc:IceServers:1:Username"] = "turn-user",
            ["WebRtc:IceServers:1:Credential"] = "turn-password"
        }).Build();
        var hub = CreateHub(context, member.Id, out _, configuration: config);
        var outsiderHub = CreateHub(context, stranger.Id, out _);

        var servers = await hub.GetIceServers(conversationId);

        Assert.Equal(2, servers.Count);
        Assert.Equal("turn:turn.example.test:3478", servers[1].Urls[0]);
        await Assert.ThrowsAsync<HubException>(() => outsiderHub.GetIceServers(conversationId));
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = Guid.NewGuid().ToString("N")[..20],
        DisplayName = "Test",
        PasswordHash = "test-hash"
    };

    private static Friendship CreateFriendship(User first, User second)
    {
        var firstIsLow = string.CompareOrdinal(first.Id.ToString("N"), second.Id.ToString("N")) < 0;
        return new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = first.Id,
            AddresseeId = second.Id,
            PairUserLowId = firstIsLow ? first.Id : second.Id,
            PairUserHighId = firstIsLow ? second.Id : first.Id,
            Requester = first,
            Addressee = second,
            Status = FriendshipStatus.Accepted
        };
    }

    private static ChatHub CreateHub(
        AppDbContext context,
        Guid userId,
        out TestHubClients clients,
        CallSessionTracker? callSessions = null,
        IConnectionTracker? connectionTracker = null,
        IConfiguration? configuration = null)
    {
        clients = new TestHubClients();
        var notifier = new TestChatNotifier();
        return new ChatHub(
            context,
            new ConversationService(context, notifier),
            connectionTracker ?? new ConnectionTracker(),
            callSessions ?? new CallSessionTracker(),
            configuration ?? new ConfigurationBuilder().Build())
        {
            Context = new TestHubCallerContext(userId),
            Clients = clients
        };
    }

    private sealed class TestHubCallerContext(Guid userId) : HubCallerContext
    {
        public override string ConnectionId => "connection";
        public override string? UserIdentifier => userId.ToString();
        public override ClaimsPrincipal? User { get; } = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class TestHubClients : IHubCallerClients
    {
        public List<SentMessage> Messages { get; } = [];

        public IClientProxy All => Proxy("all");
        public IClientProxy Caller => Proxy("caller");
        public IClientProxy Others => Proxy("others");

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all-except");
        public IClientProxy Client(string connectionId) => Proxy(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");
        public IClientProxy Group(string groupName) => Proxy(groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");
        public IClientProxy User(string userId) => Proxy(userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");
        public IClientProxy OthersInGroup(string groupName) => Proxy(groupName);

        private IClientProxy Proxy(string destination) => new TestClientProxy(destination, Messages);
    }

    private sealed class TestClientProxy(string destination, List<SentMessage> messages) : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[]? args,
            CancellationToken cancellationToken = default)
        {
            messages.Add(new SentMessage(destination, method));
            return Task.CompletedTask;
        }
    }

    private sealed record SentMessage(string Destination, string Method);

    private sealed class TestChatNotifier : IChatNotifier
    {
        public Task ConversationCreatedAsync(Guid userId, Connext.Shared.DTOs.Conversations.ConversationSummaryDto conversation) =>
            Task.CompletedTask;

        public Task ConversationMemberAddedAsync(Guid userId, Connext.Shared.DTOs.Conversations.ConversationSummaryDto conversation) =>
            Task.CompletedTask;

        public Task ConversationMembersRemovedAsync(
            Guid conversationId,
            IReadOnlyCollection<Guid> removedUserIds,
            IReadOnlyCollection<Guid> remainingUserIds) =>
            Task.CompletedTask;

        public Task ConversationRenamedAsync(Guid conversationId, string name) =>
            Task.CompletedTask;

        public Task ConversationMemberRoleChangedAsync(Guid conversationId, Guid memberId, string role) =>
            Task.CompletedTask;

        public Task GroupAvatarUpdatedAsync(Guid conversationId, string? avatarUrl) =>
            Task.CompletedTask;

        public Task MessageReactionAddedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji) =>
            Task.CompletedTask;

        public Task MessageReactionRemovedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji) =>
            Task.CompletedTask;

        public Task MessagePinnedAsync(Guid conversationId, Guid messageId, Guid userId) =>
            Task.CompletedTask;

        public Task MessageUnpinnedAsync(Guid conversationId, Guid messageId, Guid userId) =>
            Task.CompletedTask;

        public Task CallHistoryRecordedAsync(
            Guid conversationId,
            Connext.Shared.DTOs.Conversations.MessageResponseDto message) =>
            Task.CompletedTask;

        public Task PollCreatedAsync(
            Guid conversationId,
            Connext.Shared.DTOs.Conversations.MessageResponseDto message) =>
            Task.CompletedTask;

        public Task PollVotedAsync(
            Guid conversationId,
            Connext.Shared.DTOs.Conversations.PollVoteUpdateDto update) =>
            Task.CompletedTask;
    }
}
