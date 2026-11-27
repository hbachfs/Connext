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
    Task ConversationMemberRoleChangedAsync(Guid conversationId, Guid memberId, string role);
    Task GroupAvatarUpdatedAsync(Guid conversationId, string? avatarUrl);
    Task MessageReactionAddedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji);
    Task MessageReactionRemovedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji);
    Task MessagePinnedAsync(Guid conversationId, Guid messageId, Guid userId);
    Task MessageUnpinnedAsync(Guid conversationId, Guid messageId, Guid userId);
    Task CallHistoryRecordedAsync(Guid conversationId, MessageResponseDto message);
    Task PollCreatedAsync(Guid conversationId, MessageResponseDto message);
    Task PollVotedAsync(Guid conversationId, PollVoteUpdateDto update);
}
