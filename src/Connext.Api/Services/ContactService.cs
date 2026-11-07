using Microsoft.Data.SqlClient;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Contacts;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public interface IContactService
{
    Task<List<UserSearchResponseDto>> SearchUsersAsync(Guid currentUserId, string query);
    Task<bool> SendFriendRequestAsync(Guid requesterId, string phoneNumber);
    Task<List<FriendResponseDto>> GetFriendsAsync(Guid userId);
    Task<bool> RespondToRequestAsync(Guid userId, Guid requestId, bool accept);
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

        var users = await _context.Users
            .Where(u => u.Id != currentUserId &&
                        (u.PhoneNumber == query || u.DisplayName.Contains(query)))
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
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        phoneNumber = phoneNumber.Trim();

        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        if (targetUser == null || targetUser.Id == requesterId)
            return false;

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => (f.RequesterId == requesterId && f.AddresseeId == targetUser.Id) ||
                                     (f.RequesterId == targetUser.Id && f.AddresseeId == requesterId));

        if (existing != null)
            return false;

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

        request.Status = accept ? FriendshipStatus.Accepted : FriendshipStatus.Declined;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
