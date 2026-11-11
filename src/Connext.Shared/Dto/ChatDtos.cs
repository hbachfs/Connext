namespace Connext.Shared.Dtos;

public record SendMessageRequest(Guid ToUserId, string Content);

public record MessageDto(
    long Id,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    DateTime SentAt,
    DateTime? ReadAt);

public record MessagePageDto(
    IReadOnlyList<MessageDto> Items,
    long? NextBeforeId);   // null = hết tin cũ

public record MarkReadRequest(long UpToMessageId);