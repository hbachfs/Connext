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
    Task<Guid> RecordDeliveredAsync(Guid userId, Guid messageId);
}
