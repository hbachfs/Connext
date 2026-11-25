// src/Connext.Api/Services/GroupService.cs
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Connext.Shared;

namespace Connext.Api.Services;

public static class GroupRules
{
    public const int MinMembers = 3;
    public const string Admin = "Admin";
    public const string Member = "Member";
}

public enum GroupError
{
    None, NotFound, NotMember, NotAdmin, InvalidTarget,
    NotFriendOrBlocked, MinMembers, LastAdmin, Invalid, TooMany
}

// Notify = mọi user cần nhận event (kể cả người vừa bị xóa/rời)
public record GroupResult(GroupError Error, IReadOnlyList<Guid> Notify, IReadOnlyList<Guid> Affected)
{
    public bool Ok => Error == GroupError.None;
    public static GroupResult Fail(GroupError e) => new(e, [], []);
}

public interface IGroupService
{
    Task<GroupResult> AddMembersAsync(Guid actorId, Guid conversationId, IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<GroupResult> RemoveMemberAsync(Guid actorId, Guid conversationId, Guid targetId, CancellationToken ct = default);
    Task<GroupResult> LeaveAsync(Guid actorId, Guid conversationId, CancellationToken ct = default);
    Task<GroupResult> UpdateInfoAsync(Guid actorId, Guid conversationId, string? name, string? avatarUrl, CancellationToken ct = default);
    Task<GroupResult> SetRoleAsync(Guid actorId, Guid conversationId, Guid targetId, string role, CancellationToken ct = default);
}

public class GroupService(AppDbContext db) : IGroupService
{
    private async Task<(Conversation? conv, ConversationMember? actor, GroupError err)> LoadAsync(
        Guid actorId, Guid conversationId, CancellationToken ct)
    {
        var conv = await db.Conversations.Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conv is null || !conv.IsGroup) return (null, null, GroupError.NotFound);

        var actor = conv.Members.FirstOrDefault(m => m.UserId == actorId);
        return actor is null ? (conv, null, GroupError.NotMember) : (conv, actor, GroupError.None);
    }

    private static GroupResult Ok(Conversation c, IEnumerable<Guid> notifyExtra, IEnumerable<Guid> affected) =>
        new(GroupError.None, c.Members.Select(m => m.UserId).Concat(notifyExtra).Distinct().ToList(), affected.ToList());

    // ---------------- Thêm thành viên (chỉ admin, chỉ bạn bè chưa bị chặn) ----------------
    public async Task<GroupResult> AddMembersAsync(Guid actorId, Guid conversationId, IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var (conv, actor, err) = await LoadAsync(actorId, conversationId, ct);
        if (err != GroupError.None) return GroupResult.Fail(err);
        // SỬA: đồng bộ với controller, chỉ admin được thêm thành viên
        if (actor!.Role != GroupRules.Admin) return GroupResult.Fail(GroupError.NotAdmin);

        var existing = conv!.Members.Select(m => m.UserId).ToHashSet();
        var ids = userIds.Distinct().Where(id => !existing.Contains(id)).ToList();
        if (ids.Count == 0) return GroupResult.Fail(GroupError.Invalid);
        if (conv.Members.Count + ids.Count > Limits.MaxGroupMembers) return GroupResult.Fail(GroupError.TooMany);

        // Phải là bạn của người thêm
        var friendIds = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == actorId && ids.Contains(f.AddresseeId)) ||
                 (f.AddresseeId == actorId && ids.Contains(f.RequesterId))))
            .Select(f => f.RequesterId == actorId ? f.AddresseeId : f.RequesterId)
            .ToListAsync(ct);
        if (friendIds.Distinct().Count() != ids.Count) return GroupResult.Fail(GroupError.NotFriendOrBlocked);

        // Không chặn nhau (2 chiều)
        var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(b =>
            (b.BlockerId == actorId && ids.Contains(b.BlockedUserId)) ||
            (b.BlockedUserId == actorId && ids.Contains(b.BlockerId)), ct);
        if (blocked) return GroupResult.Fail(GroupError.NotFriendOrBlocked);

        var now = DateTime.UtcNow; // SỬA: dùng chung 1 mốc thời gian cho mọi thành viên mới
        foreach (var id in ids)
            conv.Members.Add(new ConversationMember { UserId = id, Role = GroupRules.Member, JoinedAt = now });

        await db.SaveChangesAsync(ct);
        return Ok(conv, [], ids);
    }

    // ---------------- Xóa thành viên ----------------
    public async Task<GroupResult> RemoveMemberAsync(Guid actorId, Guid conversationId, Guid targetId, CancellationToken ct = default)
    {
        var (conv, actor, err) = await LoadAsync(actorId, conversationId, ct);
        if (err != GroupError.None) return GroupResult.Fail(err);
        if (actor!.Role != GroupRules.Admin) return GroupResult.Fail(GroupError.NotAdmin);
        if (targetId == actorId) return GroupResult.Fail(GroupError.InvalidTarget); // tự rời: dùng Leave

        var target = conv!.Members.FirstOrDefault(m => m.UserId == targetId);
        if (target is null) return GroupResult.Fail(GroupError.InvalidTarget);
        if (target.Role == GroupRules.Admin) return GroupResult.Fail(GroupError.InvalidTarget);
        // SỬA: cùng quy tắc với controller (còn lại phải >= 3), thay cho Count <= MinMembers
        if (conv.Members.Count - 1 < GroupRules.MinMembers) return GroupResult.Fail(GroupError.MinMembers);

        var notify = conv.Members.Select(m => m.UserId).ToList(); // gồm cả người bị xóa
        db.ConversationMembers.Remove(target);
        await db.SaveChangesAsync(ct);
        return new GroupResult(GroupError.None, notify, [targetId]);
    }

    // ---------------- Rời nhóm ----------------
    public async Task<GroupResult> LeaveAsync(Guid actorId, Guid conversationId, CancellationToken ct = default)
    {
        var (conv, actor, err) = await LoadAsync(actorId, conversationId, ct);
        if (err != GroupError.None) return GroupResult.Fail(err);

        var notify = conv!.Members.Select(m => m.UserId).ToList();

        if (conv.Members.Count - 1 < Limits.MinGroupMembers)   // SỬA: giải tán
        {
            db.ConversationMembers.RemoveRange(conv.Members);
            await db.SaveChangesAsync(ct);
            return new GroupResult(GroupError.None, notify, notify);
        }

        if (actor!.Role == GroupRules.Admin && conv.Members.Count(m => m.Role == GroupRules.Admin) == 1)
            conv.Members.Where(m => m.UserId != actorId).OrderBy(m => m.JoinedAt).First().Role = GroupRules.Admin;

        db.ConversationMembers.Remove(actor);
        await db.SaveChangesAsync(ct);
        return new GroupResult(GroupError.None, notify, [actorId]);
    }

    // ---------------- Đổi tên, avatar (chỉ admin) ----------------
    public async Task<GroupResult> UpdateInfoAsync(Guid actorId, Guid conversationId, string? name, string? avatarUrl, CancellationToken ct = default)
    {
        var (conv, actor, err) = await LoadAsync(actorId, conversationId, ct);
        if (err != GroupError.None) return GroupResult.Fail(err);
        // SỬA: chỉ admin được đổi tên/avatar (đồng bộ với controller)
        if (actor!.Role != GroupRules.Admin) return GroupResult.Fail(GroupError.NotAdmin);

        if (name is not null)
        {
            name = name.Trim();
            if (name.Length is 0 or > 100) return GroupResult.Fail(GroupError.Invalid);
            conv!.Name = name;
        }
        if (avatarUrl is not null)
        {
            // Chỉ nhận file đã upload qua /api/files/upload (giống ChatHub)
            if (!avatarUrl.StartsWith("/uploads/", StringComparison.Ordinal) || avatarUrl.Length > 300 || avatarUrl.Contains(".."))
                return GroupResult.Fail(GroupError.Invalid);
            conv!.AvatarUrl = avatarUrl;
        }

        await db.SaveChangesAsync(ct);
        return Ok(conv!, [], []);
    }

    // ---------------- Phân quyền ----------------
    public async Task<GroupResult> SetRoleAsync(Guid actorId, Guid conversationId, Guid targetId, string role, CancellationToken ct = default)
    {
        var (conv, actor, err) = await LoadAsync(actorId, conversationId, ct);
        if (err != GroupError.None) return GroupResult.Fail(err);
        if (actor!.Role != GroupRules.Admin) return GroupResult.Fail(GroupError.NotAdmin);
        if (role != GroupRules.Admin && role != GroupRules.Member) return GroupResult.Fail(GroupError.Invalid);

        var target = conv!.Members.FirstOrDefault(m => m.UserId == targetId);
        if (target is null) return GroupResult.Fail(GroupError.InvalidTarget);

        if (target.Role == GroupRules.Admin && role == GroupRules.Member &&
            conv.Members.Count(m => m.Role == GroupRules.Admin) == 1)
            return GroupResult.Fail(GroupError.LastAdmin);

        target.Role = role;
        await db.SaveChangesAsync(ct);
        return Ok(conv, [], [targetId]);
    }
   
}