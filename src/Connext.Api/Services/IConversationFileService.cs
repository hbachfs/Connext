using Connext.Shared.DTOs.Conversations;

namespace Connext.Api.Services;

public interface IConversationFileService
{
    Task<ConversationMediaDto> UploadAsync(Guid userId, Guid conversationId, IFormFile file, CancellationToken cancellationToken);
    Task<List<ConversationMediaDto>> GetMediaAsync(Guid userId, Guid conversationId, string? kind);
    Task<(string FileName, long Size)?> GetDownloadInfoAsync(Guid userId, Guid conversationId, Guid fileId);
    Task DownloadAsync(Guid userId, Guid conversationId, Guid fileId, Stream destination, CancellationToken cancellationToken);
}
