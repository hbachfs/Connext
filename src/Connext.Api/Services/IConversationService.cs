using Connext.Shared.DTOs.Conversations;

namespace Connext.Api.Services;

public interface IConversationService
{
    Task<List<ConversationSummaryDto>> GetConversationsAsync(Guid userId);
    Task<ConversationSummaryDto> CreateGroupAsync(Guid creatorId, CreateGroupConversationDto request);
    Task<List<MessageResponseDto>> GetMessagesAsync(Guid userId, Guid conversationId, int pageSize);
    Task MarkReadAsync(Guid userId, Guid conversationId);
    Task<bool> IsMemberAsync(Guid userId, Guid conversationId);
    Task<MessageResponseDto> SendMessageAsync(Guid userId, SendMessageDto request);
    Task<PollResponseDto> CreatePollAsync(Guid userId, Guid conversationId, CreatePollDto request);
    Task<PollResponseDto> GetPollAsync(Guid userId, Guid conversationId, Guid pollId);
    Task<PollResponseDto> VotePollAsync(Guid userId, Guid conversationId, Guid pollId, Guid optionId);
    Task<MessageResponseDto> RecordCallHistoryAsync(
        Guid conversationId,
        Guid callerId,
        string result,
        TimeSpan? duration);
    Task<Guid> RecordDeliveredAsync(Guid userId, Guid messageId);
    Task<ConversationSummaryDto> CreateDirectAsync(Guid userId, Guid friendId);
    Task AddMemberAsync(Guid userId, Guid conversationId, Guid memberId);
    Task RemoveMemberAsync(Guid userId, Guid conversationId, Guid memberId);
    Task LeaveGroupAsync(Guid userId, Guid conversationId);
    Task RenameGroupAsync(Guid userId, Guid conversationId, string name);
    Task SetGroupAvatarAsync(Guid userId, Guid conversationId, string? avatarUrl);
    Task ChangeMemberRoleAsync(Guid userId, Guid conversationId, Guid memberId, string role);
    Task RecallMessageAsync(Guid userId, Guid conversationId, Guid messageId);
    Task AddReactionAsync(Guid userId, Guid conversationId, Guid messageId, string emoji);
    Task RemoveReactionAsync(Guid userId, Guid conversationId, Guid messageId, string emoji);
    Task PinMessageAsync(Guid userId, Guid conversationId, Guid messageId);
    Task UnpinMessageAsync(Guid userId, Guid conversationId, Guid messageId);
    Task<List<MessageResponseDto>> GetPinnedMessagesAsync(Guid userId, Guid conversationId);
}
