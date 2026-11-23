using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Connext.Api.Services;

public static class AuthRateLimitPolicy
{
    public const string Name = "auth";

    public static RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}
