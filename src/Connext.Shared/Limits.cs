namespace Connext.Shared;

public static class Limits
{
    public const long MaxImageBytes = 15L * 1024 * 1024;
    public const long MaxVideoBytes = 100L * 1024 * 1024;                  
    public const long MaxRequestBytes = MaxVideoBytes + 10L * 1024 * 1024; 

    public const int MaxMessageLength = 2000;
    public static readonly TimeSpan RecallWindow = TimeSpan.FromHours(24);
    public const int MaxPinsPerConversation = 5;
    public const int MinGroupMembers = 3;
    public const int MaxGroupMembers = 50;
    public static readonly TimeSpan FriendRequestCooldown = TimeSpan.FromHours(24);

    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(3);
    public const int OtpMaxFailedAttempts = 5;
    public static readonly TimeSpan OtpLockDuration = TimeSpan.FromMinutes(30);

    public static readonly string[] ReactionTypes = ["Like", "Love", "Haha", "Wow", "Sad", "Angry"];

    public const int PollQuestionMaxLength = 200;
    public const int PollOptionMaxLength = 100;
    public const int PollMinOptions = 2;
    public const int PollMaxOptions = 10;
}