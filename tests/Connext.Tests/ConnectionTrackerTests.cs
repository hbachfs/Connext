using Connext.Api.Hubs;
using Connext.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Connext.Tests;

public class ConnectionTrackerTests
{
    [Fact]
    public void AddAndRemove_ReportsOnlyFirstAndLastConnectionTransitions()
    {
        var tracker = new ConnectionTracker();
        var userId = Guid.NewGuid();

        Assert.True(tracker.Add(userId, "connection-1"));
        Assert.False(tracker.Add(userId, "connection-2"));
        Assert.Equal(2, tracker.GetConnections(userId).Count);
        Assert.False(tracker.Remove(userId, "connection-1"));
        Assert.True(tracker.Remove(userId, "connection-2"));
        Assert.Empty(tracker.GetConnections(userId));
    }

    [Fact]
    public async Task ChatNotifier_RemovesEveryActiveConnectionForRemovedMembers()
    {
        var tracker = new ConnectionTracker();
        var removedUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        tracker.Add(removedUserId, "connection-1");
        tracker.Add(removedUserId, "connection-2");
        tracker.Add(otherUserId, "connection-3");
        var groups = new TestGroupManager();
        var clients = new TestHubClients();
        var notifier = new ChatNotifier(new TestHubContext(groups, clients), tracker);
        var conversationId = Guid.NewGuid();

        await notifier.ConversationMembersRemovedAsync(conversationId, [removedUserId], [otherUserId]);

        Assert.Equal(
            new[]
            {
                ("connection-1", ChatHub.ConversationRoom(conversationId)),
                ("connection-2", ChatHub.ConversationRoom(conversationId))
            },
            groups.RemovedConnections);
        Assert.Contains(clients.SentEvents, message =>
            message.Destination == ChatHub.UserRoom(removedUserId) &&
            message.Method == "ConversationRemoved");
    }

    [Fact]
    public async Task ChatNotifier_AddsEveryActiveConnectionForNewMembers()
    {
        var tracker = new ConnectionTracker();
        var userId = Guid.NewGuid();
        tracker.Add(userId, "connection-1");
        tracker.Add(userId, "connection-2");
        var groups = new TestGroupManager();
        var clients = new TestHubClients();
        var notifier = new ChatNotifier(new TestHubContext(groups, clients), tracker);
        var conversation = new Connext.Shared.DTOs.Conversations.ConversationSummaryDto
        {
            Id = Guid.NewGuid(),
            Name = "Team",
            IsGroup = true
        };

        await notifier.ConversationMemberAddedAsync(userId, conversation);

        Assert.Equal(
            new[]
            {
                ("connection-1", ChatHub.ConversationRoom(conversation.Id)),
                ("connection-2", ChatHub.ConversationRoom(conversation.Id))
            },
            groups.AddedConnections);
        Assert.Contains(clients.SentEvents, message =>
            message.Destination == ChatHub.UserRoom(userId) &&
            message.Method == "ConversationCreated");
        Assert.Contains(clients.SentEvents, message =>
            message.Destination == ChatHub.ConversationRoom(conversation.Id) &&
            message.Method == "ConversationMemberAdded");
    }

    [Fact]
    public async Task ChatNotifier_BroadcastsPollEventsToTheConversationRoom()
    {
        var conversationId = Guid.NewGuid();
        var groups = new TestGroupManager();
        var clients = new TestHubClients();
        var notifier = new ChatNotifier(new TestHubContext(groups, clients), new ConnectionTracker());
        var message = new Connext.Shared.DTOs.Conversations.MessageResponseDto
        {
            Id = Guid.NewGuid(),
            Type = Connext.Shared.DTOs.Conversations.MessageType.Poll
        };
        var poll = new Connext.Shared.DTOs.Conversations.PollResponseDto { Id = Guid.NewGuid() };
        var voteUpdate = new Connext.Shared.DTOs.Conversations.PollVoteUpdateDto
        {
            Poll = poll,
            UserId = Guid.NewGuid(),
            OptionId = Guid.NewGuid()
        };

        message.Poll = poll;
        await notifier.PollCreatedAsync(conversationId, message);
        await notifier.PollVotedAsync(conversationId, voteUpdate);

        Assert.Contains(clients.SentEvents, message =>
            message.Destination == ChatHub.ConversationRoom(conversationId) &&
            message.Method == "PollCreated");
        Assert.Contains(clients.SentEvents, message =>
            message.Destination == ChatHub.ConversationRoom(conversationId) &&
            message.Method == "PollVoted");
    }

    private sealed class TestHubContext(IGroupManager groups, IHubClients clients) : IHubContext<ChatHub>
    {
        public IHubClients Clients { get; } = clients;
        public IGroupManager Groups { get; } = groups;
    }

    private sealed class TestHubClients : IHubClients
    {
        public List<(string Destination, string Method)> SentEvents { get; } = [];

        public IClientProxy All => Proxy("all");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all-except");
        public IClientProxy Client(string connectionId) => Proxy(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");
        public IClientProxy Group(string groupName) => Proxy(groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");
        public IClientProxy User(string userId) => Proxy(userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");

        private IClientProxy Proxy(string destination) => new TestClientProxy(destination, SentEvents);
    }

    private sealed class TestClientProxy(
        string destination,
        List<(string Destination, string Method)> sentEvents) : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[]? args,
            CancellationToken cancellationToken = default)
        {
            sentEvents.Add((destination, method));
            return Task.CompletedTask;
        }
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedConnections { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedConnections { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            AddedConnections.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            RemovedConnections.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }
}
