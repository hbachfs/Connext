using System.Text.RegularExpressions;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class ConversationService : IConversationService
{
    private const int MaxPageSize = 50;
    private const int MaxGroupMembers = 50;
    private const int MaxMessageLength = 2000;
    private const int MaxPinnedMessages = 5;
    private const int MaxRecallHours = 24;
    private static readonly HashSet<string> AllowedReactions =
        new(["Like", "Love", "Haha", "Wow", "Sad", "Angry"], StringComparer.OrdinalIgnoreCase);
    private static readonly Regex MentionAllRegex = new(
        @"(?<![\p{L}\p{N}_])@all(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly AppDbContext _context;
    private readonly IChatNotifier _chatNotifier;

    public ConversationService(AppDbContext context, IChatNotifier chatNotifier)
    {
        _context = context;
        _chatNotifier = chatNotifier;
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync(Guid userId)
    {
        var result = await _context.ConversationMembers
            .Where(member => member.UserId == userId)
            .Select(member => new ConversationSummaryDto
            {
                Id = member.ConversationId,
                Name = member.Conversation.IsGroup
                    ? member.Conversation.Name!
                    : member.Conversation.Members
                        .Where(other => other.UserId != userId)
                        .Select(other => other.User.DisplayName)
                        .FirstOrDefault()!,
                IsGroup = member.Conversation.IsGroup,
                AvatarUrl = member.Conversation.AvatarUrl,
                CreatedAt = member.Conversation.CreatedAt,
                LastMessage = member.Conversation.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => new MessageSummaryDto
                    {
                        Id = message.Id,
                        SenderId = message.SenderId,
                        Content = message.Content,
                        Type = message.Type,
                        CreatedAt = message.CreatedAt
                    })
                    .FirstOrDefault(),
                UnreadCount = member.Conversation.Messages.Count(message =>
                    message.SenderId != userId &&
                    (member.LastReadAt == null || message.CreatedAt > member.LastReadAt))
            })
            .ToListAsync();

        return result.OrderByDescending(conversation =>
            conversation.LastMessage?.CreatedAt ?? conversation.CreatedAt).ToList();
    }

    public async Task<ConversationSummaryDto> CreateGroupAsync(
        Guid creatorId,
        CreateGroupConversationDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MemberIds is null)
            throw new InvalidOperationException("Danh sách thành viên không hợp lệ.");
        var memberIds = request.MemberIds.Distinct().ToList();
        if (memberIds.Contains(creatorId) || memberIds.Count < 2)
            throw new InvalidOperationException("Nhóm cần ít nhất ba người, bao gồm người tạo.");
        if (memberIds.Count + 1 > MaxGroupMembers)
            throw new InvalidOperationException($"Nhóm không được vượt quá {MaxGroupMembers} thành viên.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tên nhóm không được để trống.");

        var friends = await _context.Friendships
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted &&
                ((friendship.RequesterId == creatorId && memberIds.Contains(friendship.AddresseeId)) ||
                 (friendship.AddresseeId == creatorId && memberIds.Contains(friendship.RequesterId))))
            .Select(friendship => friendship.RequesterId == creatorId
                ? friendship.AddresseeId
                : friendship.RequesterId)
            .ToListAsync();
        if (friends.Count != memberIds.Count || await HasBlockedPairAsync(creatorId, memberIds))
            throw new InvalidOperationException("Chỉ có thể thêm người dùng đã là bạn và chưa bị chặn.");

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            IsGroup = true,
            CreatedById = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        var allMemberIds = memberIds.Append(creatorId).ToList();
        conversation.Members = allMemberIds.Select(userId => new ConversationMember
        {
            ConversationId = conversation.Id,
            UserId = userId,
            JoinedAt = conversation.CreatedAt,
            Role = userId == creatorId ? ConversationMemberRole.Admin : ConversationMemberRole.Member
        }).ToList();

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var summary = new ConversationSummaryDto
        {
            Id = conversation.Id,
            Name = conversation.Name,
            IsGroup = true,
            CreatedAt = conversation.CreatedAt,
            UnreadCount = 0
        };
        foreach (var userId in allMemberIds)
            await _chatNotifier.ConversationCreatedAsync(userId, summary);

        return summary;
    }

    public async Task<List<MessageResponseDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        int pageSize)
    {
        if (pageSize is < 1 or > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"pageSize must be between 1 and {MaxPageSize}.");
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");

        var messages = await _context.Messages
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(pageSize)
            .Include(message => message.Sender)
            .Include(message => message.Receipts)
            .Include(message => message.Reactions)
            .Include(message => message.Mentions)
            .Include(message => message.Poll)
                .ThenInclude(poll => poll!.Options)
            .Include(message => message.Poll)
                .ThenInclude(poll => poll!.Votes)
            .Include(message => message.ReplyToMessage)
                .ThenInclude(reply => reply!.Sender)
            .ToListAsync();

        return messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id)
            .Select(message => ToResponse(message, userId)).ToList();
    }

    public async Task MarkReadAsync(Guid userId, Guid conversationId)
    {
        var member = await _context.ConversationMembers.FindAsync(conversationId, userId);
        if (member is null)
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        member.LastReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public Task<bool> IsMemberAsync(Guid userId, Guid conversationId) =>
        _context.ConversationMembers.AnyAsync(member =>
            member.ConversationId == conversationId && member.UserId == userId);

    public async Task<MessageResponseDto> SendMessageAsync(Guid userId, SendMessageDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = request.Content?.Trim();
        if (request.ConversationId == Guid.Empty || string.IsNullOrEmpty(content) ||
            content.Length > MaxMessageLength ||
            request.Type is not (MessageType.Text or MessageType.Image or MessageType.File))
            throw new InvalidOperationException("Nội dung hoặc loại tin nhắn không hợp lệ.");

        var conversation = await _context.Conversations
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == request.ConversationId);
        if (conversation is null || conversation.Members.All(member => member.UserId != userId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var otherMemberIds = conversation.Members
            .Where(member => member.UserId != userId)
            .Select(member => member.UserId)
            .ToList();
        if (await HasBlockedPairAsync(userId, otherMemberIds))
            throw new InvalidOperationException("Không thể nhắn tin do có người dùng bị chặn.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = userId,
            Content = content,
            Type = request.Type,
            CreatedAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId
        };
        if (request.MentionedUserIds.Any(id => !conversation.Members.Any(member => member.UserId == id)))
            throw new InvalidOperationException("Chỉ có thể nhắc thành viên trong cuộc hội thoại.");
        var mentionedUserIds = request.MentionedUserIds.Distinct().ToList();
        if (conversation.IsGroup && MentionAllRegex.IsMatch(content))
            mentionedUserIds = mentionedUserIds
                .Concat(conversation.Members.Where(member => member.UserId != userId).Select(member => member.UserId))
                .Distinct()
                .ToList();
        if (await HasBlockedPairAsync(userId, mentionedUserIds))
            throw new InvalidOperationException("Không thể nhắc người dùng đã bị chặn.");
        if (request.ReplyToMessageId.HasValue)
        {
            var repliedTo = await _context.Messages.FirstOrDefaultAsync(item =>
                item.Id == request.ReplyToMessageId.Value &&
                item.ConversationId == request.ConversationId &&
                !item.IsDeleted);
            if (repliedTo is null)
                throw new InvalidOperationException("Tin nhắn được trả lời không tồn tại.");
        }
        _context.Messages.Add(message);
        foreach (var mentionedUserId in mentionedUserIds)
            message.Mentions.Add(new MessageMention
            {
                MessageId = message.Id,
                UserId = mentionedUserId
            });
        await _context.SaveChangesAsync();

        await _context.Entry(message).Reference(item => item.Sender).LoadAsync();
        if (message.ReplyToMessageId.HasValue)
        {
            await _context.Entry(message).Reference(item => item.ReplyToMessage).LoadAsync();
            await _context.Entry(message.ReplyToMessage!).Reference(item => item.Sender).LoadAsync();
        }
        return ToResponse(message);
    }

    public async Task<PollResponseDto> CreatePollAsync(
        Guid userId,
        Guid conversationId,
        CreatePollDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var question = request.Question?.Trim();
        var options = request.Options?.Select(option => option?.Trim()).ToList();
        if (string.IsNullOrWhiteSpace(question) || question.Length > 200 ||
            options is null || options.Count is < 2 or > 10 ||
            options.Any(string.IsNullOrWhiteSpace) ||
            options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            throw new InvalidOperationException("Câu hỏi hoặc lựa chọn bình chọn không hợp lệ.");

        var conversation = await _context.Conversations
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId);
        if (conversation is null || !conversation.IsGroup ||
            conversation.Members.All(member => member.UserId != userId))
            throw new InvalidOperationException("Chỉ thành viên của nhóm mới có thể tạo bình chọn.");

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            Content = question,
            Type = MessageType.Poll,
            CreatedAt = now
        };
        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            MessageId = message.Id,
            Message = message,
            Question = question,
            CreatedAt = now,
            Options = options.Select(text => new PollOption
            {
                Id = Guid.NewGuid(),
                Text = text!
            }).ToList()
        };
        message.Poll = poll;
        _context.Messages.Add(message);
        _context.Polls.Add(poll);
        await _context.SaveChangesAsync();

        var result = ToPollResponse(poll, userId, conversationId);
        await _context.Entry(message).Reference(item => item.Sender).LoadAsync();
        await _chatNotifier.PollCreatedAsync(conversationId, ToResponse(message, userId));
        return result;
    }

    public async Task<PollResponseDto> GetPollAsync(Guid userId, Guid conversationId, Guid pollId)
    {
        var poll = await GetGroupPollAsync(userId, conversationId, pollId);
        return ToPollResponse(poll, userId, conversationId);
    }

    public async Task<PollResponseDto> VotePollAsync(
        Guid userId,
        Guid conversationId,
        Guid pollId,
        Guid optionId)
    {
        var poll = await GetGroupPollAsync(userId, conversationId, pollId);
        var option = poll.Options.FirstOrDefault(candidate => candidate.Id == optionId);
        if (option is null)
            throw new InvalidOperationException("Lựa chọn không thuộc bình chọn này.");

        var vote = poll.Votes.FirstOrDefault(candidate => candidate.UserId == userId);
        if (vote?.OptionId == optionId)
            return ToPollResponse(poll, userId, conversationId);

        if (vote is null)
        {
            poll.Votes.Add(new PollVote
            {
                PollId = poll.Id,
                UserId = userId,
                OptionId = optionId,
                VotedAt = DateTime.UtcNow
            });
        }
        else
        {
            vote.OptionId = optionId;
            vote.VotedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var result = ToPollResponse(poll, userId, conversationId);
        var update = new PollVoteUpdateDto
        {
            Poll = ToPollResponse(poll, Guid.Empty, conversationId),
            UserId = userId,
            OptionId = optionId
        };
        await _chatNotifier.PollVotedAsync(conversationId, update);
        return result;
    }

    public async Task<MessageResponseDto> RecordCallHistoryAsync(
        Guid conversationId,
        Guid callerId,
        string result,
        TimeSpan? duration)
    {
        var conversation = await _context.Conversations
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId && !item.IsGroup);
        if (conversation is null || conversation.Members.All(member => member.UserId != callerId))
            throw new InvalidOperationException("Không thể ghi lịch sử cuộc gọi.");
        if (string.IsNullOrWhiteSpace(result) || result.Length > 100)
            throw new InvalidOperationException("Kết quả cuộc gọi không hợp lệ.");

        var content = duration.HasValue
            ? $"{result} ({(int)duration.Value.TotalMinutes:00}:{duration.Value.Seconds:00})"
            : result;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = callerId,
            Content = content,
            Type = MessageType.Call,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        await _context.Entry(message).Reference(item => item.Sender).LoadAsync();
        var response = ToResponse(message);
        await _chatNotifier.CallHistoryRecordedAsync(conversationId, response);
        return response;
    }

    public async Task<ConversationSummaryDto> CreateDirectAsync(Guid userId, Guid friendId)
    {
        if (userId == friendId)
            throw new InvalidOperationException("Không thể tạo cuộc hội thoại với chính mình.");
        var friendship = await _context.Friendships.AnyAsync(item =>
            item.Status == FriendshipStatus.Accepted &&
            ((item.RequesterId == userId && item.AddresseeId == friendId) ||
             (item.RequesterId == friendId && item.AddresseeId == userId)));
        if (!friendship || await HasBlockedPairAsync(userId, [friendId]))
            throw new InvalidOperationException("Chỉ có thể trò chuyện với bạn bè chưa bị chặn.");

        var existing = await _context.Conversations
            .Where(conversation => !conversation.IsGroup &&
                conversation.Members.Any(member => member.UserId == userId) &&
                conversation.Members.Any(member => member.UserId == friendId))
            .FirstOrDefaultAsync();
        if (existing is not null &&
            await _context.ConversationMembers.CountAsync(member => member.ConversationId == existing.Id) != 2)
            existing = null;
        if (existing is not null)
            return await BuildSummaryAsync(existing);

        var friend = await _context.Users.FindAsync(friendId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            Members =
            [
                new ConversationMember { ConversationId = Guid.Empty, UserId = userId },
                new ConversationMember { ConversationId = Guid.Empty, UserId = friendId }
            ]
        };
        foreach (var member in conversation.Members)
            member.ConversationId = conversation.Id;
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
        var summary = await BuildSummaryAsync(conversation);
        foreach (var memberId in new[] { userId, friendId })
            await _chatNotifier.ConversationCreatedAsync(memberId, summary);
        return summary;
    }


    public async Task AddMemberAsync(Guid userId, Guid conversationId, Guid memberId)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (!conversation.IsGroup)
            throw new InvalidOperationException("Không thể thêm thành viên vào cuộc hội thoại 1-1.");
        if (conversation.Members.Any(member => member.UserId == memberId))
            throw new InvalidOperationException("Người dùng đã là thành viên.");
        if (conversation.Members.Count >= MaxGroupMembers)
            throw new InvalidOperationException($"Nhóm không được vượt quá {MaxGroupMembers} thành viên.");
        var friend = await _context.Friendships.AnyAsync(item =>
            item.Status == FriendshipStatus.Accepted &&
            ((item.RequesterId == userId && item.AddresseeId == memberId) ||
             (item.RequesterId == memberId && item.AddresseeId == userId)));
        if (!friend || await HasBlockedPairAsync(userId, [memberId]))
            throw new InvalidOperationException("Chỉ có thể thêm bạn bè chưa bị chặn.");
        _context.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversationId,
            UserId = memberId,
            JoinedAt = DateTime.UtcNow,
            Role = ConversationMemberRole.Member
        });
        await _context.SaveChangesAsync();
        await _chatNotifier.ConversationMemberAddedAsync(memberId, await BuildSummaryAsync(conversation));
    }

    public async Task RemoveMemberAsync(Guid userId, Guid conversationId, Guid memberId)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (!conversation.IsGroup)
            throw new InvalidOperationException("Không thể xóa thành viên khỏi cuộc hội thoại 1-1.");
        if (memberId == userId)
            throw new InvalidOperationException("Không thể tự xóa bằng API này.");
        var member = conversation.Members.FirstOrDefault(item => item.UserId == memberId);
        if (member is null)
            throw new InvalidOperationException("Người dùng không thuộc nhóm.");
        if (IsLastAdmin(conversation, member))
            throw new InvalidOperationException("Nhóm phải có ít nhất một quản trị viên.");
        conversation.Members.Remove(member);
        _context.ConversationMembers.Remove(member);
        await _context.SaveChangesAsync();
        await _chatNotifier.ConversationMembersRemovedAsync(
            conversationId,
            [memberId],
            conversation.Members.Select(item => item.UserId).ToArray());
    }

    public async Task LeaveGroupAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _context.Conversations.Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId && item.IsGroup);
        var member = conversation?.Members.FirstOrDefault(item => item.UserId == userId);
        if (conversation is null || member is null)
            throw new InvalidOperationException("Bạn không thuộc nhóm này.");
        var conversationWillBeDeleted = conversation.Members.Count - 1 < 3;
        var removedUserIds = new List<Guid> { userId };
        ConversationMember? promotedMember = null;
        if (!conversationWillBeDeleted && conversation.CreatedById == userId)
        {
            promotedMember = conversation.Members
                .Where(item => item.UserId != userId)
                .OrderBy(item => item.Role == ConversationMemberRole.Admin ? 0 : 1)
                .ThenBy(item => item.JoinedAt)
                .ThenBy(item => item.UserId)
                .First();
            promotedMember.Role = ConversationMemberRole.Admin;
            conversation.CreatedById = promotedMember.UserId;
        }
        else if (!conversationWillBeDeleted && IsLastAdmin(conversation, member))
        {
            throw new InvalidOperationException("Hãy chuyển quyền quản trị trước khi rời nhóm.");
        }
        conversation.Members.Remove(member);
        _context.ConversationMembers.Remove(member);
        var remainingUserIds = conversation.Members.Select(item => item.UserId).ToList();
        if (conversationWillBeDeleted)
        {
            removedUserIds.AddRange(conversation.Members.Select(item => item.UserId));
            remainingUserIds.Clear();
            _context.Conversations.Remove(conversation);
        }
        await _context.SaveChangesAsync();
        if (promotedMember is not null)
            await _chatNotifier.ConversationMemberRoleChangedAsync(
                conversationId, promotedMember.UserId, ConversationMemberRole.Admin.ToString());
        await _chatNotifier.ConversationMembersRemovedAsync(conversationId, removedUserIds, remainingUserIds);
    }

    public async Task RenameGroupAsync(Guid userId, Guid conversationId, string name)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new InvalidOperationException("Tên nhóm không hợp lệ.");
        conversation.Name = name.Trim();
        await _context.SaveChangesAsync();
        await _chatNotifier.ConversationRenamedAsync(conversationId, conversation.Name);
    }

    public async Task SetGroupAvatarAsync(Guid userId, Guid conversationId, string? avatarUrl)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (!conversation.IsGroup)
            throw new InvalidOperationException("Chỉ nhóm mới có ảnh đại diện nhóm.");
        if (avatarUrl?.Trim().Length > 500)
            throw new InvalidOperationException("Đường dẫn ảnh đại diện không hợp lệ.");
        conversation.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        await _context.SaveChangesAsync();
        await _chatNotifier.GroupAvatarUpdatedAsync(conversationId, conversation.AvatarUrl);
    }

    public async Task ChangeMemberRoleAsync(Guid userId, Guid conversationId, Guid memberId, string role)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (!Enum.TryParse<ConversationMemberRole>(role, true, out var newRole) || !Enum.IsDefined(newRole))
            throw new InvalidOperationException("Vai trò không hợp lệ.");
        var member = conversation.Members.FirstOrDefault(item => item.UserId == memberId);
        if (member is null)
            throw new InvalidOperationException("Người dùng không thuộc nhóm.");
        if (member.Role != newRole &&
            newRole != ConversationMemberRole.Admin &&
            IsLastAdmin(conversation, member))
            throw new InvalidOperationException("Nhóm phải có ít nhất một quản trị viên.");
        if (member.Role == newRole)
            return;
        member.Role = newRole;
        await _context.SaveChangesAsync();
        await _chatNotifier.ConversationMemberRoleChangedAsync(
            conversationId, memberId, newRole.ToString());
    }

    public async Task RecallMessageAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var message = await _context.Messages.FirstOrDefaultAsync(item =>
            item.Id == messageId && item.ConversationId == conversationId);
        if (message is null || message.SenderId != userId)
            throw new InvalidOperationException("Chỉ người gửi mới có thể thu hồi tin nhắn.");
        if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromHours(MaxRecallHours))
            throw new InvalidOperationException("Đã hết thời gian thu hồi tin nhắn.");
        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.Content = "Tin nhắn đã được thu hồi";
        await _context.SaveChangesAsync();
    }

    public async Task AddReactionAsync(Guid userId, Guid conversationId, Guid messageId, string emoji)
    {
        var message = await _context.Messages.FirstOrDefaultAsync(item =>
            item.Id == messageId && item.ConversationId == conversationId);
        if (message is null || !await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Không thể thêm phản ứng.");
        emoji = NormalizeReaction(emoji);
        var existingReactions = await _context.MessageReactions
            .Where(item => item.MessageId == messageId && item.UserId == userId)
            .ToListAsync();
        var existingReaction = existingReactions.FirstOrDefault(item => item.Emoji == emoji);
        if (existingReaction is not null && existingReactions.Count == 1)
            return;

        foreach (var reaction in existingReactions.Where(item => item.Emoji != emoji))
            _context.MessageReactions.Remove(reaction);
        if (existingReaction is null)
        {
            _context.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji
            });
        }
        await _context.SaveChangesAsync();
        foreach (var reaction in existingReactions.Where(item => item.Emoji != emoji))
            await _chatNotifier.MessageReactionRemovedAsync(conversationId, messageId, userId, reaction.Emoji);
        if (existingReaction is null)
            await _chatNotifier.MessageReactionAddedAsync(conversationId, messageId, userId, emoji);
    }

    public async Task RemoveReactionAsync(Guid userId, Guid conversationId, Guid messageId, string emoji)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        emoji = NormalizeReaction(emoji);
        var reaction = await _context.MessageReactions.FirstOrDefaultAsync(item =>
            item.MessageId == messageId &&
            item.Message.ConversationId == conversationId &&
            item.UserId == userId &&
            item.Emoji == emoji);
        if (reaction is null)
            throw new InvalidOperationException("Phản ứng không tồn tại.");
        _context.MessageReactions.Remove(reaction);
        await _context.SaveChangesAsync();
        await _chatNotifier.MessageReactionRemovedAsync(conversationId, messageId, userId, emoji);
    }

    public async Task PinMessageAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var message = await _context.Messages.FirstOrDefaultAsync(item =>
            item.Id == messageId && item.ConversationId == conversationId);
        if (message is null)
            throw new InvalidOperationException("Tin nhắn không tồn tại.");
        if (message.IsPinned)
            return;
        var pinnedCount = await _context.Messages.CountAsync(item =>
            item.ConversationId == conversationId && item.IsPinned);
        if (pinnedCount >= MaxPinnedMessages)
            throw new InvalidOperationException($"Cuộc hội thoại đã đạt giới hạn {MaxPinnedMessages} tin ghim.");
        message.IsPinned = true;
        message.PinnedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _chatNotifier.MessagePinnedAsync(conversationId, messageId, userId);
    }

    public async Task UnpinMessageAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var message = await _context.Messages.FirstOrDefaultAsync(item =>
            item.Id == messageId && item.ConversationId == conversationId);
        if (message is null)
            throw new InvalidOperationException("Tin nhắn không tồn tại.");
        message.IsPinned = false;
        message.PinnedAt = null;
        await _context.SaveChangesAsync();
        await _chatNotifier.MessageUnpinnedAsync(conversationId, messageId, userId);
    }

    public async Task<List<MessageResponseDto>> GetPinnedMessagesAsync(Guid userId, Guid conversationId)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var messages = await _context.Messages
            .Where(message => message.ConversationId == conversationId && message.IsPinned)
            .Include(message => message.Sender)
            .Include(message => message.Receipts)
            .Include(message => message.Reactions)
            .Include(message => message.Mentions)
            .Include(message => message.Poll)
                .ThenInclude(poll => poll!.Options)
            .Include(message => message.Poll)
                .ThenInclude(poll => poll!.Votes)
            .OrderByDescending(message => message.PinnedAt)
            .ToListAsync();
        return messages.Select(message => ToResponse(message, userId)).ToList();
    }

    public async Task<Guid> RecordDeliveredAsync(Guid userId, Guid messageId)
    {
        var message = await _context.Messages
            .Include(item => item.Conversation)
            .ThenInclude(conversation => conversation.Members)
            .Include(item => item.Receipts)
            .FirstOrDefaultAsync(item => item.Id == messageId);
        if (message is null ||
            message.SenderId == userId ||
            message.Conversation.Members.All(member => member.UserId != userId))
            throw new InvalidOperationException("Không thể xác nhận tin nhắn này.");

        if (message.Receipts.All(receipt => receipt.UserId != userId))
        {
            _context.MessageReceipts.Add(new MessageReceipt
            {
                MessageId = messageId,
                UserId = userId,
                DeliveredAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        return message.SenderId;
    }

    private Task<bool> HasBlockedPairAsync(Guid userId, IReadOnlyCollection<Guid> otherUserIds) =>
        _context.UserBlocks.AnyAsync(block =>
            otherUserIds.Contains(block.BlockerId) && block.BlockedUserId == userId ||
            block.BlockerId == userId && otherUserIds.Contains(block.BlockedUserId));

    private async Task<Poll> GetGroupPollAsync(Guid userId, Guid conversationId, Guid pollId)
    {
        var isGroupMember = await _context.Conversations.AnyAsync(conversation =>
            conversation.Id == conversationId &&
            conversation.IsGroup &&
            conversation.Members.Any(member => member.UserId == userId));
        if (!isGroupMember)
            throw new InvalidOperationException("Bạn không thuộc nhóm này.");

        return await _context.Polls
            .Include(poll => poll.Options)
            .Include(poll => poll.Votes)
            .FirstOrDefaultAsync(poll =>
                poll.Id == pollId && poll.Message.ConversationId == conversationId)
            ?? throw new InvalidOperationException("Không tìm thấy bình chọn.");
    }

    private static string NormalizeReaction(string reaction)
    {
        var normalized = reaction?.Trim();
        if (string.IsNullOrEmpty(normalized))
            throw new InvalidOperationException("Phản ứng không hợp lệ.");
        var canonical = AllowedReactions.FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase));
        return canonical ?? throw new InvalidOperationException(
            "Phản ứng phải là Like, Love, Haha, Wow, Sad hoặc Angry.");
    }

    private static bool IsLastAdmin(Conversation conversation, ConversationMember member) =>
        IsAdmin(conversation, member) &&
        !conversation.Members.Any(other =>
            other.UserId != member.UserId && IsAdmin(conversation, other));

    private static bool IsAdmin(Conversation conversation, ConversationMember member) =>
        member.Role == ConversationMemberRole.Admin || conversation.CreatedById == member.UserId;

    private async Task<Conversation> GetAdminConversationAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _context.Conversations.Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId);
        var member = conversation?.Members.FirstOrDefault(item => item.UserId == userId);
        if (conversation is null || member is null)
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        if (!conversation.IsGroup || !IsAdmin(conversation, member))
            throw new InvalidOperationException("Chỉ quản trị viên mới có quyền thực hiện.");
        return conversation;
    }

    private async Task<ConversationSummaryDto> BuildSummaryAsync(Conversation conversation)
    {
        await _context.Entry(conversation).Collection(item => item.Members).LoadAsync();
        var name = conversation.Name ?? string.Empty;
        if (!conversation.IsGroup)
        {
            var friendId = conversation.Members
                .Select(member => member.UserId)
                .FirstOrDefault(userId => userId != conversation.CreatedById);
            var friend = await _context.Users
                .Where(user => user.Id == friendId)
                .FirstOrDefaultAsync();
            name = friend?.DisplayName ?? string.Empty;
        }
        return new ConversationSummaryDto
        {
            Id = conversation.Id,
            Name = conversation.Name ?? name.ToString(),
            AvatarUrl = conversation.AvatarUrl,
            IsGroup = conversation.IsGroup,
            CreatedAt = conversation.CreatedAt,
            UnreadCount = 0
        };
    }

    private static PollResponseDto ToPollResponse(Poll poll, Guid userId, Guid conversationId)
    {
        var voteCounts = poll.Votes.GroupBy(vote => vote.OptionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var totalVotes = poll.Votes.Count;
        return new PollResponseDto
        {
            Id = poll.Id,
            ConversationId = conversationId,
            MessageId = poll.MessageId,
            Question = poll.Question,
            CreatedAt = poll.CreatedAt,
            TotalVotes = totalVotes,
            MyVoteOptionId = poll.Votes.FirstOrDefault(vote => vote.UserId == userId)?.OptionId,
            Options = poll.Options.Select(option =>
            {
                var voteCount = voteCounts.GetValueOrDefault(option.Id);
                return new PollOptionResultDto
                {
                    Id = option.Id,
                    Text = option.Text,
                    VoteCount = voteCount,
                    Percentage = totalVotes == 0 ? 0 : Math.Round((decimal)voteCount * 100 / totalVotes, 2)
                };
            }).ToList()
        };
    }

    private static MessageResponseDto ToResponse(Message message, Guid userId = default) => new()
    {
        Id = message.Id,
        SenderId = message.SenderId,
        SenderDisplayName = message.Sender?.DisplayName ?? string.Empty,
        Content = message.Content,
        Type = message.Type,
        CreatedAt = message.CreatedAt,
        DeliveredToUserIds = message.Receipts.Select(receipt => receipt.UserId).ToList(),
        ReplyToMessageId = message.ReplyToMessageId,
        ReplyToMessage = message.ReplyToMessage is null ? null : new MessageSummaryDto
        {
            Id = message.ReplyToMessage.Id,
            SenderId = message.ReplyToMessage.SenderId,
            Content = message.ReplyToMessage.IsDeleted
                ? "Tin nhắn đã được thu hồi"
                : message.ReplyToMessage.Content,
            Type = message.ReplyToMessage.Type,
            CreatedAt = message.ReplyToMessage.CreatedAt
        },
        Reactions = message.Reactions
            .GroupBy(reaction => reaction.Emoji)
            .ToDictionary(group => group.Key, group => group.Select(reaction => reaction.UserId).ToList()),
        MentionedUserIds = message.Mentions.Select(mention => mention.UserId).ToList(),
        IsPinned = message.IsPinned,
        IsDeleted = message.IsDeleted,
        EditedAt = message.EditedAt,
        Poll = message.Poll is null ? null : ToPollResponse(message.Poll, userId, message.ConversationId)
    };
}
