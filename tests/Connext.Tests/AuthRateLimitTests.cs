using System.Reflection;
using System.Net;
using System.Threading.RateLimiting;
using Connext.Api.Controllers;
using Connext.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Connext.Tests;

public class AuthRateLimitTests
{
    [Fact]
    public async Task AuthPolicy_AllowsFiveRequestsPerIpAndRejectsTheSixth()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
            AuthRateLimitPolicy.GetPartition);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        for (var request = 0; request < 5; request++)
        {
            using var lease = await limiter.AcquireAsync(context);
            Assert.True(lease.IsAcquired);
        }

        using var rejectedLease = await limiter.AcquireAsync(context);
        Assert.False(rejectedLease.IsAcquired);
    }

    [Fact]
    public void LoginEndpoint_UsesAuthRateLimitPolicy()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));

        var attribute = Assert.Single(method!.GetCustomAttributes<EnableRateLimitingAttribute>());

        Assert.Equal(AuthRateLimitPolicy.Name, attribute.PolicyName);
    }

    [Fact]
    public void VerifyOtpEndpoint_UsesAuthRateLimitPolicy()
    {
        var method = typeof(PasswordResetController).GetMethod(nameof(PasswordResetController.VerifyOtp));

        var attribute = Assert.Single(method!.GetCustomAttributes<EnableRateLimitingAttribute>());

        Assert.Equal(AuthRateLimitPolicy.Name, attribute.PolicyName);
    }
}
