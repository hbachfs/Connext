
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Connext.Api.Services;

// Chỉ đúng khi chạy 1 instance (giống presence trong ChatHub). Nhiều instance cần lưu trạng thái ngoài bộ nhớ.
public class CallManager(
    IServiceScopeFactory scopes,
    ICallNotifier notifier,
    TimeProvider clock,
    IOptions<CallOptions> options,
    ILogger<CallManager> logger)
{
    private sealed class ActiveCall
    {
        public Guid Id { get; } = Guid.NewGuid();
        public required Guid CallerId { get; init; }
        public required Guid CalleeId { get; init; }
        public required bool Video { get; init; }
        public CallStatus Status { get; set; } = CallStatus.Ringing;
        public DateTime? AnsweredAt { get; set; }
        public CancellationTokenSource Cts { get; } = new();
        public Guid Peer(Guid user) => user == CallerId ? CalleeId : CallerId;
        public bool Has(Guid user) => user == CallerId || user == CalleeId;
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, ActiveCall> _calls = new();
    private readonly Dictionary<Guid, Guid> _byUser = new();   // userId -> callId
    private readonly CallOptions _opt = options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ===================== Gọi đi =====================
    public async Task<CallResult> StartAsync(Guid callerId, Guid calleeId, bool video)
    {
        if (callerId == calleeId) return new(CallError.InvalidTarget);

        // ---- Kiểm tra quyền: bạn bè, chưa chặn (2 chiều) ----
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Users.AnyAsync(u => u.Id == calleeId)) return new(CallError.UserNotFound);

            var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(b =>
                (b.BlockerId == callerId && b.BlockedUserId == calleeId) ||
                (b.BlockerId == calleeId && b.BlockedUserId == callerId));
            if (blocked) return new(CallError.Blocked);

            var friends = await db.Friendships.AsNoTracking().AnyAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == callerId && f.AddresseeId == calleeId) ||
                 (f.RequesterId == calleeId && f.AddresseeId == callerId)));
            if (!friends) return new(CallError.NotFriends);
        }

        // ---- Máy bận ----
        bool callerBusy, calleeBusy;
        lock (_gate)
        {
            callerBusy = _byUser.ContainsKey(callerId);
            calleeBusy = _byUser.ContainsKey(calleeId);
        }
        if (callerBusy) return new(CallError.Busy);
        if (calleeBusy)
        {
            await LogAsync(callerId, calleeId, video, CallEndReason.Missed, 0);   // người nhận thấy cuộc gọi nhỡ
            return new(CallError.Busy);
        }

        // ---- Không trực tuyến: ghi cuộc gọi nhỡ ----
        if (!notifier.IsOnline(calleeId))
        {
            await LogAsync(callerId, calleeId, video, CallEndReason.Missed, 0);
            return new(CallError.Offline);
        }

        // ---- Đăng ký cuộc gọi (kiểm tra lại trong lock để chống 2 cuộc gọi cùng lúc) ----
        var call = new ActiveCall { CallerId = callerId, CalleeId = calleeId, Video = video };
        lock (_gate)
        {
            if (_byUser.ContainsKey(callerId) || _byUser.ContainsKey(calleeId)) return new(CallError.Busy);
            _calls[call.Id] = call;
            _byUser[callerId] = call.Id;
            _byUser[calleeId] = call.Id;
        }

        await notifier.SendAsync([calleeId], "IncomingCall",
            new { CallId = call.Id, CallerId = callerId, Video = video });

        // Không trả lời sau RingTimeout => cuộc gọi nhỡ
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_opt.RingTimeout, call.Cts.Token);
                await TimeoutAsync(call.Id);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { logger.LogError(ex, "Lỗi xử lý hết thời gian đổ chuông {CallId}", call.Id); }
        });

        return new(CallError.None, call.Id);
    }

    // ===================== Chấp nhận =====================
    public async Task<CallResult> AcceptAsync(Guid userId, Guid callId)
    {
        ActiveCall call;
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out call!) || !call.Has(userId)) return new(CallError.NotFound);
            if (call.CalleeId != userId) return new(CallError.NotAllowed);
            if (call.Status != CallStatus.Ringing) return new(CallError.InvalidState);

            call.Status = CallStatus.Active;
            call.AnsweredAt = Now;
            call.Cts.Cancel();   // dừng bộ đếm đổ chuông
        }

        // Cả hai bên (mọi tab) nhận: người gọi bắt đầu tạo offer, tab khác của người nhận ngừng đổ chuông
        await notifier.SendAsync([call.CallerId, call.CalleeId], "CallAccepted",
            new { CallId = call.Id, ByUserId = userId, call.Video });
        return new(CallError.None, call.Id);
    }

    // ===================== Từ chối =====================
    public async Task<CallResult> RejectAsync(Guid userId, Guid callId)
    {
        ActiveCall call;
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out call!) || !call.Has(userId)) return new(CallError.NotFound);
            if (call.CalleeId != userId) return new(CallError.NotAllowed);
            if (call.Status != CallStatus.Ringing) return new(CallError.InvalidState);
            Remove(call);
        }
        await FinishAsync(call, CallEndReason.Rejected);
        return new(CallError.None, call.Id);
    }

    // ===================== Kết thúc / hủy =====================
    public async Task<CallResult> EndAsync(Guid userId, Guid callId)
    {
        ActiveCall call;
        CallEndReason reason;
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out call!) || !call.Has(userId)) return new(CallError.NotFound);

            reason = call.Status == CallStatus.Active ? CallEndReason.Ended
                   : call.CallerId == userId ? CallEndReason.Cancelled     // người gọi hủy khi đang đổ chuông
                   : CallEndReason.Rejected;                                // người nhận bấm "kết thúc" khi đang đổ chuông
            Remove(call);
        }
        await FinishAsync(call, reason);
        return new(CallError.None, call.Id);
    }

    // ===================== Trao đổi SDP / ICE =====================
    // kind: "offer" | "answer" | "ice"
    public async Task<CallResult> RelayAsync(Guid userId, Guid callId, string kind, string data)
    {
        if (string.IsNullOrEmpty(data)) return new(CallError.InvalidState);
        if (data.Length > _opt.MaxSignalLength) return new(CallError.TooLarge);

        Guid peer;
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out var call) || !call.Has(userId)) return new(CallError.NotFound);
            if (call.Status != CallStatus.Active) return new(CallError.InvalidState);
            peer = call.Peer(userId);
        }

        await notifier.SendAsync([peer], "ReceiveSignal",
            new { CallId = callId, FromUserId = userId, Kind = kind, Data = data });
        return new(CallError.None, callId);
    }

    // ===================== Hết thời gian đổ chuông =====================
    public async Task TimeoutAsync(Guid callId)
    {
        ActiveCall? call;
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out call) || call.Status != CallStatus.Ringing) return;
            Remove(call);
        }
        await FinishAsync(call, CallEndReason.Missed);
    }

    // ===================== Mất kết nối (kết nối cuối của user đã đóng) =====================
    public async Task HandleUserOfflineAsync(Guid userId)
    {
        ActiveCall? call;
        CallEndReason reason;
        lock (_gate)
        {
            if (!_byUser.TryGetValue(userId, out var id) || !_calls.TryGetValue(id, out call)) return;

            reason = call.Status == CallStatus.Active ? CallEndReason.ConnectionLost
                   : call.CallerId == userId ? CallEndReason.Cancelled
                   : CallEndReason.Missed;
            Remove(call);
        }
        await FinishAsync(call, reason);
    }

    // Trạng thái hiện tại (dùng cho test / debug)
    public bool IsInCall(Guid userId) { lock (_gate) return _byUser.ContainsKey(userId); }

    // ===================== nội bộ =====================
    private void Remove(ActiveCall call)   // gọi trong lock
    {
        _calls.Remove(call.Id);
        _byUser.Remove(call.CallerId);
        _byUser.Remove(call.CalleeId);
        call.Cts.Cancel();
    }

    private async Task FinishAsync(ActiveCall call, CallEndReason reason)
    {
        var seconds = call.AnsweredAt is { } t ? Math.Max(0, (int)(Now - t).TotalSeconds) : 0;

        await notifier.SendAsync([call.CallerId, call.CalleeId], "CallEnded",
            new { CallId = call.Id, Reason = reason.ToString(), DurationSeconds = seconds });

        try { await LogAsync(call.CallerId, call.CalleeId, call.Video, reason, seconds); }
        catch (Exception ex) { logger.LogError(ex, "Không ghi được lịch sử cuộc gọi {CallId}", call.Id); }
    }

    // Ghi lịch sử cuộc gọi thành tin nhắn hệ thống trong hội thoại 1-1
    private async Task LogAsync(Guid callerId, Guid calleeId, bool video, CallEndReason reason, int seconds)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var direct = scope.ServiceProvider.GetRequiredService<IDirectConversationService>();

        var (conv, _) = await direct.EnsureAsync(callerId, calleeId);

        var msg = new Message
        {
            ConversationId = conv.Id,
            SenderId = callerId,
            Type = "system",
            Content = Describe(video, reason, seconds),
            Status = "Sent"
        };
        db.Messages.Add(msg);

        // Có tin mới thì hội thoại hiện lại với cả hai người
        var members = await db.ConversationMembers.Where(m => m.ConversationId == conv.Id).ToListAsync();
        foreach (var m in members) m.IsHidden = false;
        await db.SaveChangesAsync();

        foreach (var uid in new[] { callerId, calleeId })
            await notifier.SendAsync([uid], "ReceiveMessage", MessageMapper.ToDto(msg, uid == callerId));
    }

    public static string Describe(bool video, CallEndReason reason, int seconds)
    {
        var kind = video ? "video" : "thoại";
        var answered = seconds > 0;
        return reason switch
        {
            CallEndReason.Ended or CallEndReason.ConnectionLost when answered =>
                reason == CallEndReason.ConnectionLost
                    ? $"Cuộc gọi {kind} bị gián đoạn · {FormatDuration(seconds)}"
                    : $"Cuộc gọi {kind} · {FormatDuration(seconds)}",
            CallEndReason.Rejected => $"Cuộc gọi {kind} bị từ chối",
            _ => $"Cuộc gọi {kind} nhỡ"
        };
    }

    public static string FormatDuration(int seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}