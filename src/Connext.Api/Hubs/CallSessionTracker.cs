namespace Connext.Api.Hubs;

public sealed record ActiveCall(
    Guid ConversationId,
    Guid CallerId,
    Guid RecipientId,
    DateTime StartedAt,
    DateTime? ConnectedAt);

public class CallSessionTracker
{
    public static readonly TimeSpan RingTimeout = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, ActiveCall> _callsByConversation = [];
    private readonly Dictionary<Guid, Guid> _conversationByUser = [];

    public bool TryStart(Guid conversationId, Guid callerId, Guid recipientId, DateTime now)
    {
        lock (_gate)
        {
            if (_conversationByUser.ContainsKey(callerId) ||
                _conversationByUser.ContainsKey(recipientId) ||
                _callsByConversation.ContainsKey(conversationId))
                return false;

            var call = new ActiveCall(conversationId, callerId, recipientId, now, null);
            _callsByConversation.Add(conversationId, call);
            _conversationByUser.Add(callerId, conversationId);
            _conversationByUser.Add(recipientId, conversationId);
            return true;
        }
    }

    public bool TryAnswer(
        Guid conversationId,
        Guid callerId,
        Guid responderId,
        bool accepted,
        DateTime now,
        out ActiveCall call)
    {
        lock (_gate)
        {
            if (!TryGetCall(conversationId, callerId, responderId, out call) ||
                call.ConnectedAt.HasValue ||
                now - call.StartedAt >= RingTimeout)
                return false;

            if (accepted)
                _callsByConversation[conversationId] = call with { ConnectedAt = now };
            else
                RemoveCall(call);
            return true;
        }
    }

    public bool TryEnd(Guid conversationId, Guid userId, Guid peerId, out ActiveCall call)
    {
        lock (_gate)
        {
            if (!TryGetCall(conversationId, userId, peerId, out call))
                return false;

            RemoveCall(call);
            return true;
        }
    }

    public bool CanRelaySignal(Guid conversationId, Guid senderId, Guid recipientId, string type)
    {
        lock (_gate)
        {
            if (!TryGetCall(conversationId, senderId, recipientId, out var call))
                return false;
            if (!call.ConnectedAt.HasValue && DateTime.UtcNow - call.StartedAt >= RingTimeout)
                return false;

            if (type.Equals("ice", StringComparison.OrdinalIgnoreCase))
                return true;
            return type.Equals("offer", StringComparison.OrdinalIgnoreCase)
                ? senderId == call.CallerId
                : call.ConnectedAt.HasValue && senderId == call.RecipientId;
        }
    }

    public IReadOnlyList<ActiveCall> ExpireRingingCalls(DateTime now)
    {
        lock (_gate)
        {
            var expiredCalls = _callsByConversation.Values
                .Where(call => !call.ConnectedAt.HasValue && now - call.StartedAt >= RingTimeout)
                .ToList();
            foreach (var call in expiredCalls)
                RemoveCall(call);
            return expiredCalls;
        }
    }

    public IReadOnlyList<ActiveCall> EndCallsForUser(Guid userId)
    {
        lock (_gate)
        {
            if (!_conversationByUser.TryGetValue(userId, out var conversationId) ||
                !_callsByConversation.TryGetValue(conversationId, out var call))
                return [];

            RemoveCall(call);
            return [call];
        }
    }

    private bool TryGetCall(Guid conversationId, Guid firstUserId, Guid secondUserId, out ActiveCall call)
    {
        if (_callsByConversation.TryGetValue(conversationId, out var existing) &&
            (existing.CallerId == firstUserId && existing.RecipientId == secondUserId ||
             existing.CallerId == secondUserId && existing.RecipientId == firstUserId))
        {
            call = existing;
            return true;
        }

        call = null!;
        return false;
    }

    private void RemoveCall(ActiveCall call)
    {
        _callsByConversation.Remove(call.ConversationId);
        _conversationByUser.Remove(call.CallerId);
        _conversationByUser.Remove(call.RecipientId);
    }
}
