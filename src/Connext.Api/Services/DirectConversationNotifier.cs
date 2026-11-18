using Connext.Api.Hubs;
using Connext.Data.Entities;
using Microsoft.AspNetCore.SignalR;

namespace Connext.Api.Services;

public interface IDirectConversationNotifier
{
    Task NotifyCreatedAsync(Conversation conversation, CancellationToken ct = default);
}

public class DirectConversationNotifier(IHubContext<ChatHub> hub) : IDirectConversationNotifier
{
    public Task NotifyCreatedAsync(Conversation conversation, CancellationToken ct = default)
    {
        var groups = conversation.Members
            .Select(m => ChatHub.UserGroup(m.UserId))
            .ToList();

        return groups.Count == 0
            ? Task.CompletedTask
            : hub.Clients.Groups(groups).SendCoreAsync("AddedToConversation", new object[] { conversation.Id }, ct);
    }
}