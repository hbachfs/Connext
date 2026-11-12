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
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        await _context.Entry(message).Reference(item => item.Sender).LoadAsync();
        return ToResponse(message);
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

    private static MessageResponseDto ToResponse(Message message) => new()
    {
        Id = message.Id,
        SenderId = message.SenderId,
        SenderDisplayName = message.Sender?.DisplayName ?? string.Empty,
        Content = message.Content,
        Type = message.Type,
        CreatedAt = message.CreatedAt,
        DeliveredToUserIds = message.Receipts.Select(receipt => receipt.UserId).ToList()
    };
}
