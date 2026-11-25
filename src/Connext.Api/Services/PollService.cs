using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared;
using Connext.Shared.Chat;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public enum PollError { None, NotFound, NotMember, NotGroup, InvalidQuestion, InvalidOptions, InvalidOption, Recalled }

public record PollResult(PollError Error, PollDto? Poll, Message? Message, IReadOnlyList<Guid> MemberIds)
{
    public bool Ok => Error == PollError.None;
    public static PollResult Fail(PollError e) => new(e, null, null, []);
}

public static class PollMapper
{
    // p.Options và p.Votes phải được Include sẵn
    public static PollDto ToDto(Poll p, Guid conversationId, Guid? viewerId)
    {
        var total = p.Votes.Count;
        var counts = p.Votes.GroupBy(v => v.OptionId).ToDictionary(g => g.Key, g => g.Count());
        return new PollDto
        {
            PollId = p.Id,
            MessageId = p.MessageId,
            ConversationId = conversationId,
            CreatedById = p.CreatedById,
            Question = p.Question,
            CreatedAt = p.CreatedAt,
            TotalVotes = total,
            MyOptionId = viewerId is null ? null : p.Votes.FirstOrDefault(v => v.UserId == viewerId)?.OptionId,
            Options = p.Options.OrderBy(o => o.Position).Select(o =>
            {
                var c = counts.GetValueOrDefault(o.Id);
                return new PollOptionDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    VoteCount = c,
                    Percent = total == 0 ? 0 : Math.Round(c * 100.0 / total, 1)
                };
            }).ToList()
        };
    }
}

public interface IPollService
{
    Task<PollResult> CreateAsync(Guid userId, Guid conversationId, string? question, IEnumerable<string>? options, CancellationToken ct = default);
    Task<PollResult> VoteAsync(Guid userId, Guid conversationId, Guid pollId, Guid optionId, CancellationToken ct = default);
    Task<PollResult> GetAsync(Guid userId, Guid conversationId, Guid pollId, CancellationToken ct = default);
}

public class PollService(AppDbContext db) : IPollService
{
    public static readonly bool GroupOnly = true;   // false = cho phép tạo poll cả trong chat 1-1

    private async Task<List<ConversationMember>> MembersAsync(Guid conversationId, CancellationToken ct) =>
        await db.ConversationMembers.Where(cm => cm.ConversationId == conversationId).ToListAsync(ct);

    // ---------------- Tạo poll ----------------
    public async Task<PollResult> CreateAsync(Guid userId, Guid conversationId, string? question, IEnumerable<string>? options, CancellationToken ct = default)
    {
        var members = await MembersAsync(conversationId, ct);
        if (members.All(m => m.UserId != userId)) return PollResult.Fail(PollError.NotMember);

        if (GroupOnly && !await db.Conversations.AnyAsync(c => c.Id == conversationId && c.IsGroup, ct))
            return PollResult.Fail(PollError.NotGroup);

        var q = question?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length > Limits.PollQuestionMaxLength)
            return PollResult.Fail(PollError.InvalidQuestion);

        var opts = (options ?? []).Select(o => o?.Trim()).Where(o => !string.IsNullOrEmpty(o)).Select(o => o!).ToList();
        if (opts.Count < Limits.PollMinOptions || opts.Count > Limits.PollMaxOptions
            || opts.Any(o => o.Length > Limits.PollOptionMaxLength)
            || opts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != opts.Count)
            return PollResult.Fail(PollError.InvalidOptions);

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Type = "poll",
            Content = q,
            Status = "Sent"
        };
        var poll = new Poll
        {
            Message = message,
            CreatedById = userId,
            Question = q,
            Options = opts.Select((t, i) => new PollOption { Text = t, Position = i }).ToList()
        };
        db.Polls.Add(poll);   // thêm luôn Message qua quan hệ

        foreach (var m in members) m.IsHidden = false;   // có tin mới thì hội thoại hiện lại
        await db.SaveChangesAsync(ct);

        return new PollResult(PollError.None, PollMapper.ToDto(poll, conversationId, null), message,
            members.Select(m => m.UserId).ToList());
    }

    // ---------------- Tải poll + kiểm tra quyền ----------------
    private async Task<(Poll? poll, List<Guid> memberIds, PollError err)> LoadAsync(
        Guid userId, Guid conversationId, Guid pollId, CancellationToken ct)
    {
        var members = await MembersAsync(conversationId, ct);
        var me = members.FirstOrDefault(m => m.UserId == userId);
        if (me is null) return (null, [], PollError.NotMember);

        var poll = await db.Polls.Include(p => p.Options).Include(p => p.Votes).Include(p => p.Message)
            .FirstOrDefaultAsync(p => p.Id == pollId && p.Message.ConversationId == conversationId, ct);
        if (poll is null || (me.ClearedAt.HasValue && poll.Message.CreatedAt <= me.ClearedAt.Value))
            return (null, [], PollError.NotFound);

        return (poll, members.Select(m => m.UserId).ToList(), PollError.None);
    }

    // ---------------- Bình chọn / đổi lựa chọn ----------------
    public async Task<PollResult> VoteAsync(Guid userId, Guid conversationId, Guid pollId, Guid optionId, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var (poll, memberIds, err) = await LoadAsync(userId, conversationId, pollId, ct);
            if (poll is null) return PollResult.Fail(err);
            if (poll.Message.IsRecalled) return PollResult.Fail(PollError.Recalled);
            if (poll.Options.All(o => o.Id != optionId)) return PollResult.Fail(PollError.InvalidOption);

            var vote = poll.Votes.FirstOrDefault(v => v.UserId == userId);
            if (vote is null)
                poll.Votes.Add(new PollVote { PollId = poll.Id, OptionId = optionId, UserId = userId, VotedAt = DateTime.UtcNow });
            else if (vote.OptionId != optionId)
            {
                vote.OptionId = optionId;
                vote.VotedAt = DateTime.UtcNow;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Hai request vote cùng lúc đụng unique index (PollId, UserId): tải lại và thử lần nữa
                db.ChangeTracker.Clear();
                continue;
            }

            return new PollResult(PollError.None, PollMapper.ToDto(poll, conversationId, userId), poll.Message, memberIds);
        }
    }

    // ---------------- Xem poll kèm kết quả ----------------
    public async Task<PollResult> GetAsync(Guid userId, Guid conversationId, Guid pollId, CancellationToken ct = default)
    {
        var (poll, memberIds, err) = await LoadAsync(userId, conversationId, pollId, ct);
        return poll is null
            ? PollResult.Fail(err)
            : new PollResult(PollError.None, PollMapper.ToDto(poll, conversationId, userId), poll.Message, memberIds);
    }
}