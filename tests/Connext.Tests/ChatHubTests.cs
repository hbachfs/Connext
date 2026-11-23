using System.Security.Claims;
using Connext.Api.Hubs;
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = sender.Id });
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversationId, UserId = sender.Id },
            new ConversationMember { ConversationId = conversationId, UserId = recipient.Id });
        await context.SaveChangesAsync();
        var hub = CreateHub(context, sender.Id, out var clients);

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
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = sender.Id });
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversationId, UserId = sender.Id },
            new ConversationMember { ConversationId = conversationId, UserId = recipient.Id });
        await context.SaveChangesAsync();
        var hub = CreateHub(context, sender.Id, out var clients);

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

    private static ChatHub CreateHub(AppDbContext context, Guid userId, out TestHubClients clients)
    {
        clients = new TestHubClients();
        var notifier = new TestChatNotifier();
        return new ChatHub(context, new ConversationService(context, notifier), new ConnectionTracker())
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
    }
}
