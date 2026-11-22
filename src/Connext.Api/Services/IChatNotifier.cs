using Connext.Shared.DTOs.Conversations;

namespace Connext.Api.Services;

public interface IChatNotifier
{
    Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation);
}
