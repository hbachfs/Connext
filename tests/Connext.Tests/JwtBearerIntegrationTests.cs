using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Connext.Api.Hubs;
using Connext.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Connext.Tests;

public class JwtBearerIntegrationTests
{
    private const string Secret = "JwtBearerIntegrationTestSecretLongEnough123!";

    [Fact]
    public async Task ExpiredAndInvalidTokensReturnUnauthorizedForApiAndSignalR()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = Secret,
            ["JwtSettings:Issuer"] = "ConnextApi",
            ["JwtSettings:Audience"] = "ConnextWeb"
        }).Build().GetSection("JwtSettings");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => JwtBearerSetup.Configure(options, settings));
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/api/protected", () => Results.Ok()).RequireAuthorization();
        app.MapHub<SecuredTestHub>("/hubs/chat");
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var expiredToken = CreateToken(DateTime.UtcNow.AddMinutes(-1));
        var validToken = CreateToken(DateTime.UtcNow.AddMinutes(5));
        var invalidToken = TamperSignature(validToken);

        using var expiredApiRequest = new HttpRequestMessage(HttpMethod.Get, "/api/protected");
        expiredApiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        using var expiredApiResponse = await client.SendAsync(expiredApiRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredApiResponse.StatusCode);

        using var validHubResponse = await client.PostAsync(
            $"/hubs/chat/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(validToken)}",
            content: null);
        Assert.Equal(HttpStatusCode.OK, validHubResponse.StatusCode);

        foreach (var token in new[] { expiredToken, invalidToken })
        {
            using var hubResponse = await client.PostAsync(
                $"/hubs/chat/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
                content: null);
            Assert.Equal(HttpStatusCode.Unauthorized, hubResponse.StatusCode);
        }
    }

    private static string CreateToken(DateTime expiresAt)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ConnextApi",
            audience: "ConnextWeb",
            claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            expires: expiresAt,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        return string.Join('.', parts);
    }

}

[Authorize]
public sealed class SecuredTestHub : Hub
{
}
