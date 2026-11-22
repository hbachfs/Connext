namespace Connext.Api.Services;

public interface IFileStorage
{
    Task UploadAsync(string objectKey, Stream content, long size, string contentType, CancellationToken cancellationToken);
    Task DownloadAsync(string objectKey, Stream destination, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
