using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Connext.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Connext.Tests;

public class JwtBearerSetupTests
{
    private const string Secret = "JwtBearerTestSecretWhichIsLongEnough123456!";

    [Fact]
    public async Task HubToken_IsReadFromQueryOnlyOnChatHubPath()
    {
        var options = CreateOptions();
        var hubContext = new MessageReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("Bearer", "Bearer", typeof(JwtBearerHandler)),
            options);
        hubContext.Request.Path = "/hubs/chat";
        hubContext.Request.QueryString = new QueryString("?access_token=hub-token");

        await options.Events.OnMessageReceived(hubContext);

        Assert.Equal("hub-token", hubContext.Token);

        var apiContext = new MessageReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("Bearer", "Bearer", typeof(JwtBearerHandler)),
            options);
        apiContext.Request.Path = "/api/conversations";
        apiContext.Request.QueryString = new QueryString("?access_token=hub-token");
        await options.Events.OnMessageReceived(apiContext);

        Assert.Null(apiContext.Token);
    }

    [Fact]
    public void TokenValidation_RejectsExpiredJwtWithoutClockSkew()
    {
        var options = CreateOptions();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: "ConnextApi",
            audience: "ConnextWeb",
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        Assert.Throws<SecurityTokenExpiredException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                token, options.TokenValidationParameters, out _));
    }

    private static JwtBearerOptions CreateOptions()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = Secret,
            ["JwtSettings:Issuer"] = "ConnextApi",
            ["JwtSettings:Audience"] = "ConnextWeb"
        }).Build().GetSection("JwtSettings");
        var options = new JwtBearerOptions();
        JwtBearerSetup.Configure(options, settings);
        return options;
    }
}
