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
    private readonly AppDbContext _context;
    private readonly IConversationService _conversationService;
    private readonly IConnectionTracker _connectionTracker;

    public ChatHub(
        AppDbContext context,
        IConversationService conversationService,
        IConnectionTracker connectionTracker)
    {
        _context = context;
        _conversationService = conversationService;
        _connectionTracker = connectionTracker;
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
