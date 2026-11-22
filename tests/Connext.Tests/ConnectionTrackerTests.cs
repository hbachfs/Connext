using Connext.Api.Hubs;

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
}
