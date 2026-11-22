using Minio;
using Minio.DataModel.Args;

namespace Connext.Api.Services;

public class MinioFileStorage : IFileStorage
{
    private readonly IConfigurationSection _storage;
    private readonly string _bucket;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private readonly object _clientLock = new();
    private IMinioClient? _client;
    private bool _bucketReady;

    public MinioFileStorage(IConfiguration configuration)
    {
        _storage = configuration.GetSection("Minio");
        _bucket = _storage["Bucket"] ?? "connext-private";
    }

    public async Task UploadAsync(
        string objectKey,
        Stream content,
        long size,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await GetClient().PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType), cancellationToken);
    }

    public async Task DownloadAsync(string objectKey, Stream destination, CancellationToken cancellationToken)
    {
        await GetClient().GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyToAsync(destination, cancellationToken)), cancellationToken);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
        GetClient().RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectKey), cancellationToken);

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
            return;

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
                return;
            if (!await GetClient().BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(_bucket), cancellationToken))
                await GetClient().MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), cancellationToken);
            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private IMinioClient GetClient()
    {
        if (_client is not null)
            return _client;

        lock (_clientLock)
        {
            if (_client is not null)
                return _client;

            var accessKey = _storage["AccessKey"];
            var secretKey = _storage["SecretKey"];
            if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("MinIO AccessKey and SecretKey must be configured.");

            _client = new MinioClient()
                .WithEndpoint(_storage["Endpoint"] ?? "localhost:9000")
                .WithCredentials(accessKey, secretKey)
                .WithSSL(_storage.GetValue("UseSSL", false))
                .Build();
            return _client;
        }
    }
}
