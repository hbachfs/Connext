
using Connext.Shared;
namespace Connext.Api.Services;

public static class OtpPolicy
{
    public static DateTime ExpiresAt(DateTime now) => now + Limits.OtpLifetime;
    public static bool IsExpired(DateTime expiresAt, DateTime now) => now > expiresAt;
    public static bool IsLocked(DateTime? lockedUntil, DateTime now) => lockedUntil is { } t && t > now;

    public static (int failed, DateTime? lockedUntil) RegisterFailure(int failed, DateTime now) =>
        failed + 1 >= Limits.OtpMaxFailedAttempts ? (0, now + Limits.OtpLockDuration) : (failed + 1, null);
}