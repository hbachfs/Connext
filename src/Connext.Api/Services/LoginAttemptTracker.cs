using System.Security.Cryptography;
using System.Text;
using Connext.Shared;

namespace Connext.Api.Services;

public class LoginAttemptTracker
{
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);
    private const int MaxFailedAttempts = 5;
    private const int CleanupInterval = 256;

    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTime>> _attemptsByPhone = [];
    private int _operations;

    public bool IsLimited(string phoneNumber, DateTime now)
    {
        var key = GetKey(phoneNumber);
        if (key is null)
            return false;

        lock (_gate)
        {
            if (!_attemptsByPhone.TryGetValue(key, out var attempts))
                return false;
            RemoveExpired(attempts, now);
            RemoveWhenEmpty(key, attempts);
            return attempts.Count >= MaxFailedAttempts;
        }
    }

    public void RecordFailure(string phoneNumber, DateTime now)
    {
        var key = GetKey(phoneNumber);
        if (key is null)
            return;

        lock (_gate)
        {
            if (!_attemptsByPhone.TryGetValue(key, out var attempts))
            {
                attempts = new Queue<DateTime>();
                _attemptsByPhone.Add(key, attempts);
            }
            RemoveExpired(attempts, now);
            attempts.Enqueue(now);

            if (++_operations % CleanupInterval == 0)
                RemoveExpiredWindows(now);
        }
    }

    public void Reset(string phoneNumber)
    {
        var key = GetKey(phoneNumber);
        if (key is null)
            return;

        lock (_gate)
            _attemptsByPhone.Remove(key);
    }

    private static string? GetKey(string phoneNumber)
    {
        if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalized))
            return null;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private void RemoveExpiredWindows(DateTime now)
    {
        foreach (var (key, attempts) in _attemptsByPhone.ToArray())
        {
            RemoveExpired(attempts, now);
            RemoveWhenEmpty(key, attempts);
        }
    }

    private void RemoveWhenEmpty(string key, Queue<DateTime> attempts)
    {
        if (attempts.Count == 0)
            _attemptsByPhone.Remove(key);
    }

    private static void RemoveExpired(Queue<DateTime> attempts, DateTime now)
    {
        while (attempts.TryPeek(out var attempt) && now - attempt >= AttemptWindow)
            attempts.Dequeue();
    }
}
