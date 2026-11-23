using Microsoft.AspNetCore.Diagnostics;

namespace Connext.Api.Services;

public class SafeProductionExceptionHandler : IExceptionHandler
{
    private readonly ILogger<SafeProductionExceptionHandler> _logger;

    public SafeProductionExceptionHandler(ILogger<SafeProductionExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception while processing request.");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new { message = "Đã xảy ra lỗi máy chủ." }, cancellationToken);
        return true;
    }
}
