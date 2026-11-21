using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class ConversationService : IConversationService
{
    private const int MaxPageSize = 50;
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
        var memberIds = request.MemberIds.Distinct().ToList();
        if (memberIds.Contains(creatorId) || memberIds.Count < 2)
            throw new InvalidOperationException("Nhóm cần ít nhất ba người, bao gồm người tạo.");
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
            JoinedAt = conversation.CreatedAt
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
            .ToListAsync();

        return messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id)
            .Select(ToResponse).ToList();
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
            content.Length > 4000 || !Enum.IsDefined(request.Type))
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
        var mentionedUserIds = request.MentionedUserIds.Distinct()
            .Where(id => conversation.Members.Any(member => member.UserId == id))
            .ToList();
        if (request.MentionedUserIds.Any(id => !conversation.Members.Any(member => member.UserId == id)))
            throw new InvalidOperationException("Chỉ có thể nhắc thành viên trong cuộc hội thoại.");
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
        return ToResponse(message);
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
        conversation.Members.Remove(member);
        _context.ConversationMembers.Remove(member);
        await _context.SaveChangesAsync();
    }

    public async Task LeaveGroupAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _context.Conversations.Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId && item.IsGroup);
        var member = conversation?.Members.FirstOrDefault(item => item.UserId == userId);
        if (conversation is null || member is null)
            throw new InvalidOperationException("Bạn không thuộc nhóm này.");
        conversation.Members.Remove(member);
        _context.ConversationMembers.Remove(member);
        if (conversation.Members.Count < 3)
            _context.Conversations.Remove(conversation);
        await _context.SaveChangesAsync();
    }

    public async Task RenameGroupAsync(Guid userId, Guid conversationId, string name)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new InvalidOperationException("Tên nhóm không hợp lệ.");
        conversation.Name = name.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task ChangeMemberRoleAsync(Guid userId, Guid conversationId, Guid memberId, string role)
    {
        var conversation = await GetAdminConversationAsync(userId, conversationId);
        if (!Enum.TryParse<ConversationMemberRole>(role, true, out var newRole))
            throw new InvalidOperationException("Vai trò không hợp lệ.");
        var member = conversation.Members.FirstOrDefault(item => item.UserId == memberId);
        if (member is null)
            throw new InvalidOperationException("Người dùng không thuộc nhóm.");
        member.Role = newRole;
        await _context.SaveChangesAsync();
    }

    public async Task RecallMessageAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        var message = await _context.Messages.FirstOrDefaultAsync(item =>
            item.Id == messageId && item.ConversationId == conversationId);
        if (message is null || message.SenderId != userId)
            throw new InvalidOperationException("Chỉ người gửi mới có thể thu hồi tin nhắn.");
        if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromMinutes(15))
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
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Trim().Length > 32)
            throw new InvalidOperationException("Phản ứng không hợp lệ.");
        emoji = emoji.Trim();
        if (!await _context.MessageReactions.AnyAsync(item =>
                item.MessageId == messageId && item.UserId == userId && item.Emoji == emoji))
        {
            _context.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveReactionAsync(Guid userId, Guid conversationId, Guid messageId, string emoji)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        var reaction = await _context.MessageReactions.FirstOrDefaultAsync(item =>
            item.MessageId == messageId && item.UserId == userId && item.Emoji == emoji);
        if (reaction is null)
            throw new InvalidOperationException("Phản ứng không tồn tại.");
        _context.MessageReactions.Remove(reaction);
        await _context.SaveChangesAsync();
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
        if (pinnedCount >= 50)
            throw new InvalidOperationException("Cuộc hội thoại đã đạt giới hạn 50 tin ghim.");
        message.IsPinned = true;
        message.PinnedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
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
            .OrderByDescending(message => message.PinnedAt)
            .ToListAsync();
        return messages.Select(ToResponse).ToList();
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

    private async Task<Conversation> GetAdminConversationAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _context.Conversations.Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == conversationId);
        var member = conversation?.Members.FirstOrDefault(item => item.UserId == userId);
        if (conversation is null || member is null)
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");
        if (!conversation.IsGroup || member.Role != ConversationMemberRole.Admin &&
            conversation.CreatedById != userId)
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
            IsGroup = conversation.IsGroup,
            CreatedAt = conversation.CreatedAt,
            UnreadCount = 0
        };
    }

    private static MessageResponseDto ToResponse(Message message) => new()
    {
        Id = message.Id,
        SenderId = message.SenderId,
        SenderDisplayName = message.Sender?.DisplayName ?? string.Empty,
        Content = message.Content,
        Type = message.Type,
        CreatedAt = message.CreatedAt,
        DeliveredToUserIds = message.Receipts.Select(receipt => receipt.UserId).ToList(),
        ReplyToMessageId = message.ReplyToMessageId,
        Reactions = message.Reactions
            .GroupBy(reaction => reaction.Emoji)
            .ToDictionary(group => group.Key, group => group.Select(reaction => reaction.UserId).ToList()),
        MentionedUserIds = message.Mentions.Select(mention => mention.UserId).ToList(),
        IsPinned = message.IsPinned,
        IsDeleted = message.IsDeleted,
        EditedAt = message.EditedAt
    };
}
