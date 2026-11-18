
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public enum DirectError { None, UserNotFound, NotFriends, Blocked }

public record DirectResult(Conversation? Conversation, bool Created, DirectError Error)
{
    public bool Ok => Error == DirectError.None;
}

public interface IDirectConversationService
{
    Task<(Conversation Conversation, bool Created)> EnsureAsync(Guid userA, Guid userB, CancellationToken ct = default);
    Task<DirectResult> GetOrCreateForFriendAsync(Guid userId, Guid friendId, CancellationToken ct = default);
}

public class DirectConversationService(AppDbContext db) : IDirectConversationService
{
    // (A,B) và (B,A) cho cùng một key
    public static string BuildKey(Guid a, Guid b) =>
        a.CompareTo(b) < 0 ? $"{a:N}_{b:N}" : $"{b:N}_{a:N}";

    public async Task<(Conversation, bool)> EnsureAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        var key = BuildKey(userA, userB);

        var existing = await FindAsync(key, ct);
        if (existing is not null) return (existing, false);

        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            DirectKey = key,
            // Nếu Conversation có cột bắt buộc khác (CreatedAt...) thì gán thêm ở đây
            Members =
            [
                new ConversationMember { UserId = userA },
                new ConversationMember { UserId = userB }
            ]
        };

        db.Conversations.Add(conv);
        try
        {
            await db.SaveChangesAsync(ct);
            return (conv, true);
        }
        catch (DbUpdateException)
        {
            // Request song song đã tạo trước (vi phạm unique DirectKey) -> bỏ bản mình, lấy bản có sẵn
            foreach (var e in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                e.State = EntityState.Detached;

            var again = await FindAsync(key, ct);
            if (again is not null) return (again, false);
            throw;
        }
    }

    public async Task<DirectResult> GetOrCreateForFriendAsync(Guid userId, Guid friendId, CancellationToken ct = default)
    {
        if (userId == friendId) return new(null, false, DirectError.NotFriends);

        if (!await db.Users.AnyAsync(u => u.Id == friendId, ct))
            return new(null, false, DirectError.UserNotFound);

        if (await IsBlockedAsync(userId, friendId, ct))
            return new(null, false, DirectError.Blocked);

        var isFriend = await db.Friendships.AsNoTracking().AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == userId && f.AddresseeId == friendId) ||
             (f.RequesterId == friendId && f.AddresseeId == userId)), ct);
        if (!isFriend) return new(null, false, DirectError.NotFriends);

        var (conv, created) = await EnsureAsync(userId, friendId, ct);
        return new(conv, created, DirectError.None);
    }

    // TODO: nối vào bảng chặn thật của dự án (kiểm tra cả 2 chiều). Hiện develop chưa có.
    private Task<bool> IsBlockedAsync(Guid a, Guid b, CancellationToken ct) => Task.FromResult(false);

    private Task<Conversation?> FindAsync(string key, CancellationToken ct) =>
        db.Conversations.Include(c => c.Members)
          .FirstOrDefaultAsync(c => c.DirectKey == key, ct);
}