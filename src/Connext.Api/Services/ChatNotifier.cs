using Connext.Api.Hubs;
using Connext.Shared.DTOs.Conversations;
using Microsoft.AspNetCore.SignalR;

namespace Connext.Api.Services;

public class ChatNotifier : IChatNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IConnectionTracker _connectionTracker;

    public ChatNotifier(IHubContext<ChatHub> hubContext, IConnectionTracker connectionTracker)
    {
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
    }

    public async Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation)
    {
        var room = ChatHub.ConversationRoom(conversation.Id);
        foreach (var connectionId in _connectionTracker.GetConnections(userId))
            await _hubContext.Groups.AddToGroupAsync(connectionId, room);

        await _hubContext.Clients.Group(ChatHub.UserRoom(userId))
            .SendAsync("ConversationCreated", conversation);
    }

    public async Task ConversationMemberAddedAsync(Guid userId, ConversationSummaryDto conversation)
    {
        await ConversationCreatedAsync(userId, conversation);
        await _hubContext.Clients.Group(ChatHub.ConversationRoom(conversation.Id))
            .SendAsync("ConversationMemberAdded", conversation.Id, userId);
    }

    public async Task ConversationMembersRemovedAsync(
        Guid conversationId,
        IReadOnlyCollection<Guid> removedUserIds,
        IReadOnlyCollection<Guid> remainingUserIds)
    {
        var room = ChatHub.ConversationRoom(conversationId);
        foreach (var userId in removedUserIds)
        {
            foreach (var connectionId in _connectionTracker.GetConnections(userId))
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, room);

            await _hubContext.Clients.Group(ChatHub.UserRoom(userId))
                .SendAsync("ConversationRemoved", conversationId);
        }

        if (remainingUserIds.Count > 0)
            await _hubContext.Clients.Group(room)
                .SendAsync("ConversationMemberRemoved", conversationId, removedUserIds);
    }

    public Task ConversationRenamedAsync(Guid conversationId, string name) =>
        _hubContext.Clients.Group(ChatHub.ConversationRoom(conversationId))
            .SendAsync("ConversationRenamed", conversationId, name);
}
