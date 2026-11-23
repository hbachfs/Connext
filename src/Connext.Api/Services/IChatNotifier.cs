using Connext.Shared.DTOs.Conversations;

namespace Connext.Api.Services;

public interface IChatNotifier
{
    Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation);
    Task ConversationMemberAddedAsync(Guid userId, ConversationSummaryDto conversation);
    Task ConversationMembersRemovedAsync(
        Guid conversationId,
        IReadOnlyCollection<Guid> removedUserIds,
        IReadOnlyCollection<Guid> remainingUserIds);
    Task ConversationRenamedAsync(Guid conversationId, string name);
}
