using Connext.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Connext.Api.Tests;

public sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
}

public static class TestConfig
{
    public const string Key = "super-secret-key-super-secret-key-1234";

    public static IConfiguration Build(string key = Key) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = key,
            ["Jwt:Issuer"] = "connext",
            ["Jwt:Audience"] = "connext-clients",
            ["Jwt:ExpireMinutes"] = "60",
        }).Build();

    public static JwtHelper Jwt(TimeProvider time, string key = Key) => new(Build(key), time);
}
