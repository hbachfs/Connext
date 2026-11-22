
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.Chat;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public enum MsgError { None, NotFound, NotSender, WindowExpired, AlreadyRecalled, InvalidEmoji, PinLimit, NotAllowed, Recalled }

// MemberIds = mọi thành viên cần nhận event realtime
public record MsgResult(MsgError Error, Guid ConversationId, IReadOnlyList<Guid> MemberIds)
{
    public bool Ok => Error == MsgError.None;
    public static MsgResult Fail(MsgError e) => new(e, Guid.Empty, []);
}

public class MessageActionService(AppDbContext db, TimeProvider clock)
{
    // ===== Quy tắc BA: đổi ở đây =====
    public static readonly TimeSpan RecallWindow = TimeSpan.FromMinutes(15);
    public const int MaxPins = 3;
    public static readonly bool GroupPinAdminOnly = false;   // true = chỉ quản trị viên được ghim trong nhóm
    public static readonly string[] AllowedEmojis = ["👍", "❤️", "😂", "😮", "😢", "😡"];

    private record Ctx(Message Msg, ConversationMember Me, List<Guid> MemberIds, bool IsGroup);

    // Tin phải tồn tại, người dùng là thành viên, và tin nằm sau mốc xóa lịch sử (ClearedAt) của họ
    private async Task<(Ctx? ctx, MsgError err)> LoadAsync(Guid userId, long messageId, CancellationToken ct)
    {
        var msg = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return (null, MsgError.NotFound);

        var members = await db.ConversationMembers.Where(cm => cm.ConversationId == msg.ConversationId).ToListAsync(ct);
        var me = members.FirstOrDefault(m => m.UserId == userId);
        if (me is null || (me.ClearedAt.HasValue && msg.CreatedAt <= me.ClearedAt.Value))
            return (null, MsgError.NotFound);

        var isGroup = await db.Conversations.Where(c => c.Id == msg.ConversationId).Select(c => c.IsGroup).FirstAsync(ct);
        return (new Ctx(msg, me, members.Select(m => m.UserId).ToList(), isGroup), MsgError.None);
    }

    private static MsgResult Ok(Ctx c) => new(MsgError.None, c.Msg.ConversationId, c.MemberIds);

    // ---------------- Reaction: thêm / đổi ----------------
    public async Task<MsgResult> SetReactionAsync(Guid userId, long messageId, string emoji, CancellationToken ct = default)
    {
        if (!AllowedEmojis.Contains(emoji)) return MsgResult.Fail(MsgError.InvalidEmoji);

        var (ctx, err) = await LoadAsync(userId, messageId, ct);
        if (ctx is null) return MsgResult.Fail(err);
        if (ctx.Msg.IsRecalled) return MsgResult.Fail(MsgError.Recalled);

        var existing = await db.MessageReactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId, ct);
        if (existing is null)
            db.MessageReactions.Add(new MessageReaction { MessageId = messageId, UserId = userId, Emoji = emoji });
        else
            existing.Emoji = emoji;   // đổi reaction

        await db.SaveChangesAsync(ct);
        return Ok(ctx);
    }

    // ---------------- Reaction: bỏ (idempotent) ----------------
    public async Task<MsgResult> RemoveReactionAsync(Guid userId, long messageId, CancellationToken ct = default)
    {
        var (ctx, err) = await LoadAsync(userId, messageId, ct);
        if (ctx is null) return MsgResult.Fail(err);

        var existing = await db.MessageReactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId, ct);
        if (existing is not null)
        {
            db.MessageReactions.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
        return Ok(ctx);
    }

    // ---------------- Thu hồi (chỉ người gửi, trong thời gian cho phép) ----------------
    public async Task<MsgResult> RecallAsync(Guid userId, long messageId, CancellationToken ct = default)
    {
        var (ctx, err) = await LoadAsync(userId, messageId, ct);
        if (ctx is null) return MsgResult.Fail(err);

        var msg = ctx.Msg;
        if (msg.SenderId != userId) return MsgResult.Fail(MsgError.NotSender);
        if (msg.IsRecalled) return MsgResult.Fail(MsgError.AlreadyRecalled);

        var now = clock.GetUtcNow().UtcDateTime;
        if (now - msg.CreatedAt > RecallWindow) return MsgResult.Fail(MsgError.WindowExpired);

        // Xóa thật nội dung để không còn lộ qua API; hiển thị "Tin nhắn đã thu hồi" do MessageMapper
        msg.IsRecalled = true;
        msg.RecalledAt = now;
        msg.Content = null;
        msg.FileUrl = null;
        msg.FileName = null;
        msg.FileSize = null;

        db.MessageReactions.RemoveRange(await db.MessageReactions.Where(r => r.MessageId == messageId).ToListAsync(ct));
        db.MessagePins.RemoveRange(await db.MessagePins.Where(p => p.MessageId == messageId).ToListAsync(ct));

        await db.SaveChangesAsync(ct);
        return Ok(ctx);
    }

    // ---------------- Ghim ----------------
    public async Task<MsgResult> PinAsync(Guid userId, Guid conversationId, long messageId, CancellationToken ct = default)
    {
        var (ctx, err) = await LoadAsync(userId, messageId, ct);
        if (ctx is null || ctx.Msg.ConversationId != conversationId) return MsgResult.Fail(MsgError.NotFound);
        if (ctx.IsGroup && GroupPinAdminOnly && ctx.Me.Role != "Admin") return MsgResult.Fail(MsgError.NotAllowed);
        if (ctx.Msg.IsRecalled) return MsgResult.Fail(MsgError.Recalled);

        if (await db.MessagePins.AnyAsync(p => p.MessageId == messageId, ct))
            return Ok(ctx);   // đã ghim rồi: coi như thành công

        if (await db.MessagePins.CountAsync(p => p.ConversationId == conversationId, ct) >= MaxPins)
            return MsgResult.Fail(MsgError.PinLimit);

        db.MessagePins.Add(new MessagePin
        {
            MessageId = messageId,
            ConversationId = conversationId,
            PinnedById = userId,
            PinnedAt = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
        return Ok(ctx);
    }

    public async Task<MsgResult> UnpinAsync(Guid userId, Guid conversationId, long messageId, CancellationToken ct = default)
    {
        var (ctx, err) = await LoadAsync(userId, messageId, ct);
        if (ctx is null || ctx.Msg.ConversationId != conversationId) return MsgResult.Fail(MsgError.NotFound);
        if (ctx.IsGroup && GroupPinAdminOnly && ctx.Me.Role != "Admin") return MsgResult.Fail(MsgError.NotAllowed);

        var pin = await db.MessagePins.FirstOrDefaultAsync(p => p.MessageId == messageId, ct);
        if (pin is not null)
        {
            db.MessagePins.Remove(pin);
            await db.SaveChangesAsync(ct);
        }
        return Ok(ctx);
    }

    // ---------------- Danh sách tin đã ghim ----------------
    public async Task<List<PinnedMessageDto>?> GetPinsAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var me = await db.ConversationMembers.AsNoTracking()
            .FirstOrDefaultAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId, ct);
        if (me is null) return null;   // không phải thành viên

        var pins = await db.MessagePins.AsNoTracking()
            .Where(p => p.ConversationId == conversationId && !p.Message.IsRecalled &&
                        (!me.ClearedAt.HasValue || p.Message.CreatedAt > me.ClearedAt.Value))
            .OrderByDescending(p => p.PinnedAt)
            .Select(p => new { p.Message, p.PinnedById, p.PinnedAt })
            .ToListAsync(ct);

        return pins.Select(p => new PinnedMessageDto
        {
            MessageId = p.Message.Id,
            SenderId = p.Message.SenderId,
            Type = p.Message.Type,
            Preview = MessageMapper.Preview(p.Message),
            CreatedAt = p.Message.CreatedAt,
            PinnedById = p.PinnedById,
            PinnedAt = p.PinnedAt
        }).ToList();
    }
}