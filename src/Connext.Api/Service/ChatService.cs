using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class ChatService
{
    public const int PageSize = 10;
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db) => _db = db;

    public async Task<bool> AreFriendsAsync(Guid a, Guid b) =>
        await _db.Friendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == a && f.AddresseeId == b) ||
             (f.RequesterId == b && f.AddresseeId == a)));

    public async Task<Conversation> GetOrCreateConversationAsync(Guid a, Guid b)
    {
        var conv = await _db.Conversations
            .Where(c => c.Members.Count == 2 &&
                        c.Members.Any(m => m.UserId == a) &&
                        c.Members.Any(m => m.UserId == b))
            .FirstOrDefaultAsync();
        if (conv != null) return conv;

        conv = new Conversation();
        conv.Members.Add(new ConversationMember { UserId = a });
        conv.Members.Add(new ConversationMember { UserId = b });
        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync();
        return conv;
    }

    /// Trả về null nếu không phải bạn bè.
    public async Task<MessageDto?> SendAsync(Guid senderId, Guid toUserId, string content)
    {
        if (senderId == toUserId) return null;
        if (string.IsNullOrWhiteSpace(content)) return null;
        if (!await AreFriendsAsync(senderId, toUserId)) return null;

        var conv = await GetOrCreateConversationAsync(senderId, toUserId);
        var msg = new Message
        {
            ConversationId = conv.Id,
            SenderId = senderId,
            Content = content.Trim(),
            SentAt = DateTime.UtcNow
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();
        return ToDto(msg);
    }

    public Task<bool> IsMemberAsync(Guid conversationId, Guid userId) =>
        _db.ConversationMembers.AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId);

    /// Phân trang theo con trỏ: lấy PageSize tin có Id < beforeId (mới → cũ).
    public async Task<MessagePageDto?> GetMessagesAsync(Guid conversationId, Guid userId, long? beforeId)
    {
        if (!await IsMemberAsync(conversationId, userId)) return null;

        var q = _db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (beforeId.HasValue) q = q.Where(m => m.Id < beforeId.Value);

        var rows = await q.OrderByDescending(m => m.Id).Take(PageSize + 1).ToListAsync();
        var hasMore = rows.Count > PageSize;
        var page = rows.Take(PageSize).ToList();

        return new MessagePageDto(
            page.Select(ToDto).ToList(),
            hasMore ? page[^1].Id : null);
    }

    /// Đánh dấu đã xem các tin của người kia có Id <= upToMessageId. Trả về số tin được cập nhật, null nếu không phải thành viên.
    public async Task<int?> MarkReadAsync(Guid conversationId, Guid userId, long upToMessageId)
    {
        if (!await IsMemberAsync(conversationId, userId)) return null;

        var unread = await _db.Messages
            .Where(m => m.ConversationId == conversationId
                        && m.SenderId != userId
                        && m.ReadAt == null
                        && m.Id <= upToMessageId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var m in unread) m.ReadAt = now;
        await _db.SaveChangesAsync();
        return unread.Count;
    }

    public async Task<Guid?> GetOtherMemberAsync(Guid conversationId, Guid userId) =>
        await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.UserId != userId)
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync();

    private static MessageDto ToDto(Message m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.Content ?? string.Empty, m.SentAt, m.ReadAt);
}