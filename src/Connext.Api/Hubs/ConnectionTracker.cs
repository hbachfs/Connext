namespace Connext.Api.Hubs;

public interface IConnectionTracker
{
    bool Add(Guid userId, string connectionId);
    bool Remove(Guid userId, string connectionId);
    IReadOnlyList<string> GetConnections(Guid userId);
}

public class ConnectionTracker : IConnectionTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public bool Add(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
            {
                userConnections = [];
                _connections.Add(userId, userConnections);
            }

            var wasOffline = userConnections.Count == 0;
            userConnections.Add(connectionId);
            return wasOffline;
        }
    }

    public bool Remove(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
                return false;

            userConnections.Remove(connectionId);
            if (userConnections.Count != 0)
                return false;

            _connections.Remove(userId);
            return true;
        }
    }

    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        lock (_gate)
            return _connections.TryGetValue(userId, out var userConnections)
                ? userConnections.ToList()
                : [];
    }
}
