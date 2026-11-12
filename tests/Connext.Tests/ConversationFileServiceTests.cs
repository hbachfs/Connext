using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Connext.Tests;

public class ConversationFileServiceTests
{
    [Fact]
    public async Task UploadAsync_ValidatesMembershipExtensionAndFileSignature()
    {
        using var context = CreateContext();
        var memberId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        SeedConversation(context, memberId, conversationId);
        var storage = new TestFileStorage();
        var service = new ConversationFileService(context, storage);
        var validPng = CreateFormFile(
            [137, 80, 78, 71, 13, 10, 26, 10, 0],
            "photo.png",
            "text/html");

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.UploadAsync(nonMemberId, conversationId, validPng, CancellationToken.None));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.UploadAsync(memberId, conversationId, CreateFormFile([1, 2, 3], "photo.jpg", "image/jpeg"),
                CancellationToken.None));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.UploadAsync(memberId, conversationId, CreateFormFile([1, 2, 3], "attack.svg", "image/svg+xml"),
                CancellationToken.None));

        var uploaded = await service.UploadAsync(memberId, conversationId, validPng, CancellationToken.None);

        Assert.Equal("Image", uploaded.Kind);
        Assert.Equal("image/png", storage.ContentType);
        Assert.Single(await context.MediaAssets.ToListAsync());
        Assert.NotEqual("text/html", storage.ContentType);
    }

    [Fact]
    public async Task MediaGallery_ReturnsOnlyLinksAndRequiresMembership()
    {
        using var context = CreateContext();
        var memberId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        SeedConversation(context, memberId, conversationId);
        context.Messages.Add(new Message
        {
            ConversationId = conversationId,
            SenderId = memberId,
            Content = "See https://example.com/image?q=1.",
            Type = Connext.Shared.DTOs.Conversations.MessageType.Text
        });
        await context.SaveChangesAsync();
        var service = new ConversationFileService(context, new TestFileStorage());

        var links = await service.GetMediaAsync(memberId, conversationId, "Link");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMediaAsync(nonMemberId, conversationId, "Link"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetMediaAsync(memberId, conversationId, "Unknown"));

        var link = Assert.Single(links);
        Assert.Equal("Link", link.Kind);
        Assert.Equal("https://example.com/image?q=1", link.LinkUrl);
    }

    [Fact]
    public async Task UploadAsync_EnforcesLargeFileLimitBeforeWritingToStorage()
    {
        using var context = CreateContext();
        var memberId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        SeedConversation(context, memberId, conversationId);
        var storage = new TestFileStorage();
        var service = new ConversationFileService(context, storage);
        var tooLarge = new TestFormFile("video.mp4", ConversationFileService.MaxVideoBytes + 1);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.UploadAsync(memberId, conversationId, tooLarge, CancellationToken.None));

        Assert.Null(storage.ContentType);
        Assert.Empty(await context.MediaAssets.ToListAsync());
    }

    [Fact]
    public async Task DownloadInfoAsync_RequiresConversationMembership()
    {
        using var context = CreateContext();
        var memberId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        SeedConversation(context, memberId, conversationId);
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UploadedById = memberId,
            ObjectKey = $"{conversationId:N}/image.png",
            FileName = "image.png",
            Extension = ".png",
            ContentType = "image/png",
            Kind = MediaKind.Image,
            Size = 9
        };
        context.MediaAssets.Add(asset);
        await context.SaveChangesAsync();
        var service = new ConversationFileService(context, new TestFileStorage());

        Assert.Null(await service.GetDownloadInfoAsync(nonMemberId, conversationId, asset.Id));
        Assert.Equal(("image.png", 9L), await service.GetDownloadInfoAsync(memberId, conversationId, asset.Id));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void SeedConversation(AppDbContext context, Guid memberId, Guid conversationId)
    {
        var user = new User
        {
            Id = memberId,
            PhoneNumber = "+84901234567",
            DisplayName = "Member",
            PasswordHash = "test"
        };
        context.Users.Add(user);
        context.Conversations.Add(new Conversation { Id = conversationId, CreatedById = memberId });
        context.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = conversationId,
            UserId = memberId
        });
        context.SaveChanges();
    }

    private static IFormFile CreateFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TestFileStorage : IFileStorage
    {
        public string? ContentType { get; private set; }

        public Task UploadAsync(
            string objectKey,
            Stream content,
            long size,
            string contentType,
            CancellationToken cancellationToken)
        {
            ContentType = contentType;
            return Task.CompletedTask;
        }

        public Task DownloadAsync(string objectKey, Stream destination, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestFormFile(string fileName, long length) : IFormFile
    {
        public string ContentDisposition => string.Empty;
        public string ContentType => "video/mp4";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => length;
        public string Name => "file";
        public string FileName => fileName;
        public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            OpenReadStream().CopyToAsync(target, cancellationToken);
        public Stream OpenReadStream() => new MemoryStream([0, 0, 0, 24, 102, 116, 121, 112]);
    }
}
