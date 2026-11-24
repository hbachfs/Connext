using System.Security.Claims;
using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly IReadOnlyDictionary<string, string> AllowedCallSignals =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["offer"] = "offer",
            ["answer"] = "answer"
        };

    private readonly AppDbContext _context;
    private readonly IConversationService _conversationService;
    private readonly IConnectionTracker _connectionTracker;
    private readonly CallSessionTracker _callSessions;
    private readonly IConfiguration _configuration;

    public ChatHub(
        AppDbContext context,
        IConversationService conversationService,
        IConnectionTracker connectionTracker,
        CallSessionTracker callSessions,
        IConfiguration configuration)
    {
        _context = context;
        _conversationService = conversationService;
        _connectionTracker = connectionTracker;
        _callSessions = callSessions;
        _configuration = configuration;
    }

    public static string UserRoom(Guid userId) => $"user-{userId:N}";
    public static string ConversationRoom(Guid conversationId) => $"conv-{conversationId:N}";

    public override async Task OnConnectedAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            Context.Abort();
            return;
        }

        var firstConnection = _connectionTracker.Add(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserRoom(userId));

        var conversationIds = await _context.ConversationMembers
            .Where(member => member.UserId == userId)
            .Select(member => member.ConversationId)
            .ToListAsync();
        foreach (var conversationId in conversationIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationRoom(conversationId));

        var friends = await GetUnblockedFriendIdsAsync(userId);
        var onlineFriends = await _context.Users
            .Where(user => friends.Contains(user.Id) && user.IsOnline)
            .Select(user => user.Id)
            .ToListAsync();
        foreach (var friendId in onlineFriends)
            await Clients.Caller.SendAsync("OnlineChanged", friendId, true);

        if (firstConnection)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is not null)
            {
                user.IsOnline = true;
                await _context.SaveChangesAsync();
                await NotifyFriendsAsync(userId, friends, true);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId) && _connectionTracker.Remove(userId, Context.ConnectionId))
        {
            foreach (var call in _callSessions.EndCallsForUser(userId))
            {
                var peerId = call.CallerId == userId ? call.RecipientId : call.CallerId;
                await Clients.Group(UserRoom(peerId))
                    .SendAsync("CallEnded", call.ConversationId, userId);
                await RecordCallHistoryAsync(
                    call,
                    call.ConnectedAt.HasValue ? "Cuộc gọi bị ngắt kết nối" : "Cuộc gọi nhỡ");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user is not null)
            {
                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                var friends = await GetUnblockedFriendIdsAsync(userId);
                await NotifyFriendsAsync(userId, friends, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (!await _conversationService.IsMemberAsync(userId, conversationId))
            throw new HubException("Bạn không thuộc cuộc hội thoại này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationRoom(conversationId));
    }

    public async Task SendMessage(SendMessageDto request)
    {
        var userId = GetUserId();
        try
        {
            var message = await _conversationService.SendMessageAsync(userId, request);
            await Clients.Group(ConversationRoom(request.ConversationId))
                .SendAsync("ReceiveMessage", message);
            foreach (var mentionedUserId in message.MentionedUserIds)
                await Clients.Group(UserRoom(mentionedUserId))
                    .SendAsync("MessageMentioned", message);
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task Typing(Guid conversationId, bool isTyping)
    {
        var userId = GetUserId();
        if (!await _conversationService.IsMemberAsync(userId, conversationId))
            throw new HubException("Bạn không thuộc cuộc hội thoại này.");

        await Clients.GroupExcept(ConversationRoom(conversationId), Context.ConnectionId)
            .SendAsync("Typing", userId, conversationId, isTyping);
    }

    public async Task AcknowledgeDelivered(Guid messageId)
    {
        try
        {
            var senderId = await _conversationService.RecordDeliveredAsync(GetUserId(), messageId);
            await Clients.Group(UserRoom(senderId))
                .SendAsync("MessageDelivered", messageId, GetUserId());
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

        public async Task RecallMessage(Guid conversationId, Guid messageId)
        {
            try
            {
                await _conversationService.RecallMessageAsync(GetUserId(), conversationId, messageId);
                await Clients.Group(ConversationRoom(conversationId)).SendAsync("MessageRecalled", messageId);
            }
            catch (InvalidOperationException exception) { throw new HubException(exception.Message); }
        }

        public async Task AddReaction(Guid conversationId, Guid messageId, string emoji)
        {
            try
            {
                var userId = GetUserId();
                await _conversationService.AddReactionAsync(userId, conversationId, messageId, emoji);
            }
            catch (InvalidOperationException exception) { throw new HubException(exception.Message); }
        }

        public async Task RemoveReaction(Guid conversationId, Guid messageId, string emoji)
        {
            try
            {
                var userId = GetUserId();
                await _conversationService.RemoveReactionAsync(userId, conversationId, messageId, emoji);
            }
            catch (InvalidOperationException exception) { throw new HubException(exception.Message); }
        }

        public async Task InviteCall(Guid conversationId, Guid recipientId)
        {
            var userId = await EnsureCallPeerAsync(conversationId, recipientId);
            if (!_callSessions.TryStart(conversationId, userId, recipientId, DateTime.UtcNow))
            {
                await Clients.Caller.SendAsync("CallBusy", conversationId, recipientId);
                await _conversationService.RecordCallHistoryAsync(conversationId, userId, "Cuộc gọi bận", null);
                return;
            }
            if (_connectionTracker.GetConnections(recipientId).Count == 0)
            {
                _callSessions.TryEnd(conversationId, userId, recipientId, out var missedCall);
                await Clients.Caller.SendAsync("CallNoAnswer", conversationId, recipientId);
                await RecordCallHistoryAsync(missedCall, "Cuộc gọi nhỡ");
                return;
            }
            await Clients.Group(UserRoom(recipientId)).SendAsync("CallIncoming", conversationId, userId);
        }

        public async Task AnswerCall(Guid conversationId, Guid callerId, bool accepted)
        {
            var userId = await EnsureCallPeerAsync(conversationId, callerId);
            if (!_callSessions.TryAnswer(
                    conversationId, callerId, userId, accepted, DateTime.UtcNow, out var call))
                throw new HubException("Cuộc gọi không còn hoạt động.");
            await Clients.Group(UserRoom(callerId))
                .SendAsync("CallAnswered", conversationId, userId, accepted);
            if (!accepted)
                await RecordCallHistoryAsync(call, "Cuộc gọi bị từ chối");
        }

        public async Task SendIceCandidate(Guid conversationId, Guid recipientId, object candidate)
        {
            var userId = await EnsureCallPeerAsync(conversationId, recipientId);
            if (!_callSessions.CanRelaySignal(conversationId, userId, recipientId, "ice"))
                throw new HubException("Cuộc gọi không còn hoạt động.");
            await Clients.Group(UserRoom(recipientId))
                .SendAsync("IceCandidate", conversationId, userId, candidate);
        }

        public async Task SendSignal(Guid conversationId, Guid recipientId, string type, object payload)
        {
            if (string.IsNullOrWhiteSpace(type) || !AllowedCallSignals.TryGetValue(type, out var signalEvent))
                throw new HubException("Loại tín hiệu cuộc gọi không hợp lệ.");
            var userId = await EnsureCallPeerAsync(conversationId, recipientId);
            if (!_callSessions.CanRelaySignal(conversationId, userId, recipientId, type))
                throw new HubException("Tín hiệu không hợp lệ đối với trạng thái cuộc gọi.");
            await Clients.Group(UserRoom(recipientId))
                .SendAsync(signalEvent, conversationId, userId, payload);
        }

        public async Task EndCall(Guid conversationId, Guid recipientId)
        {
            var userId = await EnsureCallPeerAsync(conversationId, recipientId);
            if (!_callSessions.TryEnd(conversationId, userId, recipientId, out var call))
                throw new HubException("Cuộc gọi không còn hoạt động.");
            await Clients.Group(UserRoom(recipientId))
                .SendAsync("CallEnded", conversationId, userId);
            var result = call.ConnectedAt.HasValue ? "Cuộc gọi đã kết thúc" : "Cuộc gọi nhỡ";
            TimeSpan? duration = call.ConnectedAt.HasValue
                ? DateTime.UtcNow - call.ConnectedAt.Value
                : null;
            await RecordCallHistoryAsync(call, result, duration);
        }

        public async Task<IReadOnlyList<IceServerDto>> GetIceServers(Guid conversationId)
        {
            var userId = GetUserId();
            var memberIds = await _context.ConversationMembers
                .Where(member => member.ConversationId == conversationId)
                .Select(member => member.UserId)
                .ToListAsync();
            if (memberIds.Count != 2 || !memberIds.Contains(userId))
                throw new HubException("Chỉ thành viên hội thoại 1-1 mới truy cập được cấu hình ICE.");
            var peerId = memberIds.Single(memberId => memberId != userId);
            await EnsureCallPeerAsync(conversationId, peerId);

            var servers = _configuration.GetSection("WebRtc:IceServers").Get<List<IceServerDto>>() ?? [];
            if (servers.Count == 0 || servers.Any(server => server.Urls.Length == 0))
                throw new HubException("Máy chủ ICE chưa được cấu hình.");
            return servers;
        }

    private async Task<Guid> EnsureCallPeerAsync(Guid conversationId, Guid peerId)
    {
        var userId = GetUserId();
        if (peerId == userId ||
            !await _conversationService.IsMemberAsync(userId, conversationId) ||
            !await _conversationService.IsMemberAsync(peerId, conversationId))
            throw new HubException("Người dùng không thuộc cuộc hội thoại.");

        var isDirectConversation = await _context.Conversations.AnyAsync(conversation =>
            conversation.Id == conversationId && !conversation.IsGroup);
        var memberCount = await _context.ConversationMembers.CountAsync(member =>
            member.ConversationId == conversationId);
        var areFriends = await _context.Friendships.AnyAsync(friendship =>
            friendship.Status == FriendshipStatus.Accepted &&
            (friendship.RequesterId == userId && friendship.AddresseeId == peerId ||
             friendship.RequesterId == peerId && friendship.AddresseeId == userId));
        if (!isDirectConversation || memberCount != 2 || !areFriends)
            throw new HubException("Chỉ bạn bè trong hội thoại 1-1 mới có thể gọi.");

        var isBlocked = await _context.UserBlocks.AnyAsync(block =>
            block.BlockerId == userId && block.BlockedUserId == peerId ||
            block.BlockerId == peerId && block.BlockedUserId == userId);
        if (isBlocked)
            throw new HubException("Không thể gửi tín hiệu đến người dùng này.");

        return userId;
    }

    private async Task RecordCallHistoryAsync(
        ActiveCall call,
        string result,
        TimeSpan? duration = null)
    {
        await _conversationService.RecordCallHistoryAsync(
            call.ConversationId, call.CallerId, result, duration);
    }

    private async Task<List<Guid>> GetUnblockedFriendIdsAsync(Guid userId) =>
        await _context.Friendships
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted &&
                (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .Select(friendship => friendship.RequesterId == userId
                ? friendship.AddresseeId
                : friendship.RequesterId)
            .Where(friendId => !_context.UserBlocks.Any(block =>
                block.BlockerId == userId && block.BlockedUserId == friendId ||
                block.BlockerId == friendId && block.BlockedUserId == userId))
            .ToListAsync();

    private async Task NotifyFriendsAsync(Guid userId, IEnumerable<Guid> friendIds, bool isOnline)
    {
        foreach (var friendId in friendIds)
            await Clients.Group(UserRoom(friendId)).SendAsync("OnlineChanged", userId, isOnline);
    }

    private Guid GetUserId() =>
        TryGetUserId(out var userId) ? userId : throw new HubException("Tài khoản không hợp lệ.");

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
