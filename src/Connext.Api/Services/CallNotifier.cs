
using Connext.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Connext.Api.Services;

public interface ICallNotifier
{
    bool IsOnline(Guid userId);
    Task SendAsync(IEnumerable<Guid> userIds, string method, object payload);
}

public class HubCallNotifier(IHubContext<ChatHub> hub) : ICallNotifier
{
    public bool IsOnline(Guid userId) => ChatHub.IsUserOnline(userId);

    public Task SendAsync(IEnumerable<Guid> userIds, string method, object payload) =>
        hub.Clients.Groups(userIds.Distinct().Select(ChatHub.UserGroup).ToList()).SendAsync(method, payload);
}