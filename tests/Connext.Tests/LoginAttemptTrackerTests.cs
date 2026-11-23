using Connext.Api.Services;

namespace Connext.Tests;

public class LoginAttemptTrackerTests
{
    [Fact]
    public void FailedAttemptsAreTrackedByNormalizedPhoneAndExpireAfterOneMinute()
    {
        var tracker = new LoginAttemptTracker();
        var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        for (var attempt = 0; attempt < 5; attempt++)
            tracker.RecordFailure("0901234567", now);

        Assert.True(tracker.IsLimited("+84901234567", now));
        Assert.False(tracker.IsLimited("0901234567", now.AddMinutes(1)));
    }

    [Fact]
    public void SuccessfulAuthenticationCanResetFailedAttempts()
    {
        var tracker = new LoginAttemptTracker();
        var now = DateTime.UtcNow;
        for (var attempt = 0; attempt < 5; attempt++)
            tracker.RecordFailure("0901234567", now);

        tracker.Reset("+84901234567");

        Assert.False(tracker.IsLimited("0901234567", now));
    }
}
