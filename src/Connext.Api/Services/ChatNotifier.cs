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
}
