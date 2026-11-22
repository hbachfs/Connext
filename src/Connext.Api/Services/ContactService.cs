using Microsoft.Data.SqlClient;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Contacts;
using Connext.Shared;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public interface IContactService
{
    Task<List<UserSearchResponseDto>> SearchUsersAsync(Guid currentUserId, string query);
    Task<bool> SendFriendRequestAsync(Guid requesterId, string phoneNumber);
    Task<List<FriendResponseDto>> GetFriendsAsync(Guid userId);
    Task<bool> RespondToRequestAsync(Guid userId, Guid requestId, bool accept);
    Task<FriendshipRequestsDto> GetRequestsAsync(Guid userId);
    Task<bool> RemoveFriendAsync(Guid userId, Guid friendId);
    Task<bool> BlockUserAsync(Guid blockerId, Guid blockedUserId);
    Task<bool> UnblockUserAsync(Guid blockerId, Guid blockedUserId);
}

public class ContactService : IContactService
{
    private readonly AppDbContext _context;

    public ContactService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserSearchResponseDto>> SearchUsersAsync(Guid currentUserId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        query = query.Trim();
        var normalizedPhone = PhoneNumberNormalizer.TryNormalize(query, out var phoneNumber)
            ? phoneNumber
            : null;

        var users = await _context.Users
            .Where(u => u.Id != currentUserId &&
                        !_context.UserBlocks.Any(b =>
                            (b.BlockerId == currentUserId && b.BlockedUserId == u.Id) ||
                            (b.BlockerId == u.Id && b.BlockedUserId == currentUserId)) &&
                        ((normalizedPhone != null && u.PhoneNumber == normalizedPhone) ||
                         u.DisplayName.Contains(query)))
            .OrderBy(u => u.DisplayName)
            .Take(20)
            .ToListAsync();

        var friendshipIds = await _context.Friendships
            .Where(f => (f.RequesterId == currentUserId || f.AddresseeId == currentUserId)
                        && f.Status == FriendshipStatus.Accepted)
            .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return users.Select(u => new UserSearchResponseDto
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            PhoneNumber = u.PhoneNumber,
            AvatarUrl = u.AvatarUrl,
            IsFriend = friendshipIds.Contains(u.Id)
        }).ToList();
    }

    public async Task<bool> SendFriendRequestAsync(Guid requesterId, string phoneNumber)
    {
        if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalizedPhone))
            return false;

        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone);
        if (targetUser == null || targetUser.Id == requesterId)
            return false;

        if (await IsBlockedBetweenAsync(requesterId, targetUser.Id))
            return false;

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => (f.RequesterId == requesterId && f.AddresseeId == targetUser.Id) ||
                                     (f.RequesterId == targetUser.Id && f.AddresseeId == requesterId));

        if (existing is not null && existing.Status != FriendshipStatus.Declined)
            return false;

        if (existing?.Status == FriendshipStatus.Declined)
        {
            existing.RequesterId = requesterId;
            existing.AddresseeId = targetUser.Id;
            existing.Status = FriendshipStatus.Pending;
            existing.CreatedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        var requesterFirst = string.CompareOrdinal(
            requesterId.ToString("N"),
            targetUser.Id.ToString("N")) < 0;
        var pairUserLowId = requesterFirst ? requesterId : targetUser.Id;
        var pairUserHighId = requesterFirst ? targetUser.Id : requesterId;

        var friendship = new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = targetUser.Id,
            PairUserLowId = pairUserLowId,
            PairUserHighId = pairUserHighId,
            Status = FriendshipStatus.Pending
        };

        _context.Friendships.Add(friendship);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            _context.Entry(friendship).State = EntityState.Detached;
            return false;
        }

        return true;
    }

    public async Task<List<FriendResponseDto>> GetFriendsAsync(Guid userId)
    {
        var friends = await _context.Friendships
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) && f.Status == FriendshipStatus.Accepted)
            .Where(f => !_context.UserBlocks.Any(b =>
                (b.BlockerId == userId && b.BlockedUserId == (f.RequesterId == userId ? f.AddresseeId : f.RequesterId)) ||
                (b.BlockerId == (f.RequesterId == userId ? f.AddresseeId : f.RequesterId) && b.BlockedUserId == userId)))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return friends.Select(f => {
            var otherUser = f.RequesterId == userId ? f.Addressee : f.Requester;
            return new FriendResponseDto
            {
                Id = f.Id,
                UserId = otherUser.Id,
                DisplayName = otherUser.DisplayName,
                AvatarUrl = otherUser.AvatarUrl,
                IsOnline = otherUser.IsOnline,
                LastSeen = otherUser.LastSeen
            };
        }).ToList();
    }

    public async Task<bool> RespondToRequestAsync(Guid userId, Guid requestId, bool accept)
    {
        if (requestId == Guid.Empty)
            return false;

        var request = await _context.Friendships.FindAsync(requestId);
        if (request == null || request.AddresseeId != userId || request.Status != FriendshipStatus.Pending)
            return false;

        if (await IsBlockedBetweenAsync(userId, request.RequesterId))
            return false;

        request.Status = accept ? FriendshipStatus.Accepted : FriendshipStatus.Declined;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<FriendshipRequestsDto> GetRequestsAsync(Guid userId)
    {
        var requests = await _context.Friendships
            .Where(friendship => friendship.Status == FriendshipStatus.Pending &&
                (friendship.AddresseeId == userId || friendship.RequesterId == userId) &&
                !_context.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId ==
                        (friendship.RequesterId == userId ? friendship.AddresseeId : friendship.RequesterId)) ||
                    (block.BlockerId == (friendship.RequesterId == userId ? friendship.AddresseeId : friendship.RequesterId) &&
                        block.BlockedUserId == userId)))
            .Include(friendship => friendship.Requester)
            .Include(friendship => friendship.Addressee)
            .OrderByDescending(friendship => friendship.CreatedAt)
            .ToListAsync();

        static FriendshipRequestResponseDto ToDto(Friendship friendship, User user) => new()
        {
            RequestId = friendship.Id,
            UserId = user.Id,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = friendship.CreatedAt
        };

        return new FriendshipRequestsDto
        {
            Received = requests.Where(friendship => friendship.AddresseeId == userId)
                .Select(friendship => ToDto(friendship, friendship.Requester)).ToList(),
            Sent = requests.Where(friendship => friendship.RequesterId == userId)
                .Select(friendship => ToDto(friendship, friendship.Addressee)).ToList()
        };
    }

    public async Task<bool> RemoveFriendAsync(Guid userId, Guid friendId)
    {
        var friendship = await _context.Friendships.FirstOrDefaultAsync(candidate =>
            candidate.Status == FriendshipStatus.Accepted &&
            ((candidate.RequesterId == userId && candidate.AddresseeId == friendId) ||
             (candidate.RequesterId == friendId && candidate.AddresseeId == userId)));

        if (friendship is null)
            return false;

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BlockUserAsync(Guid blockerId, Guid blockedUserId)
    {
        if (blockerId == blockedUserId ||
            !await _context.Users.AnyAsync(user => user.Id == blockedUserId))
            return false;

        if (await _context.UserBlocks.AnyAsync(block =>
                block.BlockerId == blockerId && block.BlockedUserId == blockedUserId))
            return false;

        var friendship = await _context.Friendships.FirstOrDefaultAsync(candidate =>
            (candidate.RequesterId == blockerId && candidate.AddresseeId == blockedUserId) ||
            (candidate.RequesterId == blockedUserId && candidate.AddresseeId == blockerId));
        if (friendship is not null)
            _context.Friendships.Remove(friendship);

        _context.UserBlocks.Add(new UserBlock
        {
            BlockerId = blockerId,
            BlockedUserId = blockedUserId
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnblockUserAsync(Guid blockerId, Guid blockedUserId)
    {
        var block = await _context.UserBlocks.FindAsync(blockerId, blockedUserId);
        if (block is null)
            return false;

        _context.UserBlocks.Remove(block);
        await _context.SaveChangesAsync();
        return true;
    }

    private Task<bool> IsBlockedBetweenAsync(Guid firstUserId, Guid secondUserId) =>
        _context.UserBlocks.AnyAsync(block =>
            (block.BlockerId == firstUserId && block.BlockedUserId == secondUserId) ||
            (block.BlockerId == secondUserId && block.BlockedUserId == firstUserId));

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
