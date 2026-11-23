using Connext.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Connext.Api.Services;

public class CallTimeoutService : BackgroundService
{
    private readonly CallSessionTracker _callSessions;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallTimeoutService> _logger;

    public CallTimeoutService(
        CallSessionTracker callSessions,
        IHubContext<ChatHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<CallTimeoutService> logger)
    {
        _callSessions = callSessions;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var call in _callSessions.ExpireRingingCalls(DateTime.UtcNow))
            {
                await _hubContext.Clients.Group(ChatHub.UserRoom(call.CallerId))
                    .SendAsync("CallNoAnswer", call.ConversationId, call.RecipientId, stoppingToken);
                await _hubContext.Clients.Group(ChatHub.UserRoom(call.RecipientId))
                    .SendAsync("CallMissed", call.ConversationId, call.CallerId, stoppingToken);
                await RecordHistoryAsync(call, "Cuộc gọi nhỡ");
            }
        }
    }

    private async Task RecordHistoryAsync(ActiveCall call, string result)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            await conversationService.RecordCallHistoryAsync(
                call.ConversationId, call.CallerId, result, null);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Could not record call history for conversation {ConversationId}.",
                call.ConversationId);
        }
    }
}
