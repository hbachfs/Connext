using System.Text.Json;
using Connext.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Connext.Tests;

public class SafeProductionExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsGenericErrorAndKeepsDetailsOutOfResponse()
    {
        var handler = new SafeProductionExceptionHandler(
            NullLogger<SafeProductionExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        var exception = new InvalidOperationException("Server=internal;Password=secret");

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        response.Position = 0;
        using var body = await JsonDocument.ParseAsync(response);
        var message = body.RootElement.GetProperty("message").GetString();
        Assert.Equal("Đã xảy ra lỗi máy chủ.", message);
        Assert.DoesNotContain("secret", message);
    }
}
