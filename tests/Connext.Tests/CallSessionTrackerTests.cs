using Connext.Api.Hubs;

namespace Connext.Tests;

public class CallSessionTrackerTests
{
    [Fact]
    public void TryStart_PreventsUsersFromJoiningConcurrentCalls()
    {
        var tracker = new CallSessionTracker();
        var firstCaller = Guid.NewGuid();
        var firstRecipient = Guid.NewGuid();
        var secondRecipient = Guid.NewGuid();
        var firstConversation = Guid.NewGuid();

        Assert.True(tracker.TryStart(firstConversation, firstCaller, firstRecipient, DateTime.UtcNow));
        Assert.False(tracker.TryStart(Guid.NewGuid(), secondRecipient, firstCaller, DateTime.UtcNow));
    }

    [Fact]
    public void CallState_AllowsOfferThenAnswerAndExpiresUnansweredCalls()
    {
        var tracker = new CallSessionTracker();
        var caller = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var started = DateTime.UtcNow;

        Assert.True(tracker.TryStart(conversation, caller, recipient, started));
        Assert.True(tracker.CanRelaySignal(conversation, caller, recipient, "offer"));
        Assert.False(tracker.CanRelaySignal(conversation, recipient, caller, "answer"));
        Assert.True(tracker.TryAnswer(conversation, caller, recipient, true, started.AddSeconds(1), out _));
        Assert.True(tracker.CanRelaySignal(conversation, recipient, caller, "answer"));
        Assert.True(tracker.CanRelaySignal(conversation, caller, recipient, "ice"));
        Assert.Empty(tracker.ExpireRingingCalls(started.AddMinutes(1)));
        Assert.True(tracker.TryEnd(conversation, recipient, caller, out _));

        var nextConversation = Guid.NewGuid();
        Assert.True(tracker.TryStart(nextConversation, caller, recipient, started));
        Assert.False(tracker.TryAnswer(
            nextConversation, caller, recipient, true, started + CallSessionTracker.RingTimeout, out _));
        var expired = Assert.Single(tracker.ExpireRingingCalls(started + CallSessionTracker.RingTimeout));
        Assert.Equal(nextConversation, expired.ConversationId);
        Assert.False(tracker.TryEnd(nextConversation, caller, recipient, out _));
    }
}
