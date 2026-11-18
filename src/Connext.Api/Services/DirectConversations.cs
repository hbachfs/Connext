using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

/// <summary>Tìm hoặc tạo hội thoại 1-1 giữa 2 người (dùng chung cho FriendsController và ConversationsController).</summary>
public static class DirectConversations
{
    public static string Key(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}_{b:N}" : $"{b:N}_{a:N}";

    public static async Task<Guid> GetOrCreateAsync(AppDbContext db, Guid userId, Guid otherId)
    {
        var key = Key(userId, otherId);
        var conv = await FindAsync(db, userId, otherId, key);

        if (conv == null)
        {
            conv = new Conversation { IsGroup = false, CreatedById = userId, DirectKey = key };
            conv.Members.Add(new ConversationMember { UserId = userId, Role = "Member" });
            conv.Members.Add(new ConversationMember { UserId = otherId, Role = "Member" });
            db.Conversations.Add(conv);
            try
            {
                await db.SaveChangesAsync();
                return conv.Id;
            }
            catch (DbUpdateException ex)
            {
                // Hai request song song cùng tạo: unique index chặn bản thứ 2 -> lấy bản đã có.
                db.ChangeTracker.Clear();
                conv = await FindAsync(db, userId, otherId, key)
                       ?? throw new InvalidOperationException("Không tạo được hội thoại.", ex);
            }
        }

        // Người mở lại thì hội thoại hiện lại trong danh sách của họ (lịch sử cũ vẫn bị cắt bởi ClearedAt).
        var me = conv.Members.FirstOrDefault(m => m.UserId == userId);
        if (me is { IsHidden: true }) me.IsHidden = false;
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();

        return conv.Id;
    }

    private static async Task<Conversation?> FindAsync(AppDbContext db, Guid userId, Guid otherId, string key)
    {
        var conv = await db.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c => c.DirectKey == key);
        if (conv != null) return conv;

        // Dữ liệu cũ tạo trước khi có DirectKey
        conv = await db.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c =>
            !c.IsGroup && c.DirectKey == null &&
            c.Members.Any(m => m.UserId == userId) && c.Members.Any(m => m.UserId == otherId));
        if (conv != null) conv.DirectKey = key;
        return conv;
    }
}
