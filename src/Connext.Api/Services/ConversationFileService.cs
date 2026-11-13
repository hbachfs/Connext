using System.Text;
using System.Text.RegularExpressions;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Connext.Api.Services;

public class ConversationFileService : IConversationFileService
{
    public const long MaxImageBytes = 100L * 1024 * 1024;
    public const long MaxVideoBytes = 1024L * 1024 * 1024;
    public const long MaxDocumentBytes = 100L * 1024 * 1024;
    public const long MaxRequestBytes = MaxVideoBytes + 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly HashSet<string> DocumentExtensions = [".pdf", ".txt", ".docx", ".xlsx", ".pptx"];
    private static readonly Regex LinkRegex = new(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly IFileStorage _fileStorage;

    public ConversationFileService(AppDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<ConversationMediaDto> UploadAsync(
        Guid userId,
        Guid conversationId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            throw new ConversationFileValidationException("Tệp tải lên đang trống.");
        if (!await IsMemberAsync(userId, conversationId))
            throw new ConversationFileValidationException("Bạn không thuộc cuộc hội thoại này.");

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        var (kind, contentType, sizeLimit) = GetFileType(extension);
        if (file.Length > sizeLimit)
            throw new ConversationFileValidationException(
                $"Kích thước tệp vượt quá giới hạn {sizeLimit / 1024 / 1024} MB.");

        await using var content = file.OpenReadStream();
        if (!await HasValidSignatureAsync(content, extension, cancellationToken))
            throw new ConversationFileValidationException(
                "Nội dung tệp không khớp với phần mở rộng được khai báo.");
        content.Position = 0;

        var id = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (safeFileName.Length > 255)
            safeFileName = safeFileName[^255..];
        var objectKey = $"{conversationId:N}/{id:N}{extension}";
        await _fileStorage.UploadAsync(objectKey, content, file.Length, contentType, cancellationToken);

        var asset = new MediaAsset
        {
            Id = id,
            ConversationId = conversationId,
            UploadedById = userId,
            ObjectKey = objectKey,
            FileName = safeFileName,
            Extension = extension,
            ContentType = contentType,
            Kind = kind,
            Size = file.Length,
            CreatedAt = DateTime.UtcNow
        };
        try
        {
            _context.MediaAssets.Add(asset);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await _fileStorage.DeleteAsync(objectKey, cancellationToken);
            throw;
        }

        return ToDto(asset);
    }

    public async Task<List<ConversationMediaDto>> GetMediaAsync(
        Guid userId,
        Guid conversationId,
        string? kind)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");

        MediaKind? assetKind = kind?.ToLowerInvariant() switch
        {
            null or "" => null,
            "image" => MediaKind.Image,
            "file" => MediaKind.File,
            "link" => null,
            _ => throw new ArgumentException("kind phải là Image, File hoặc Link.", nameof(kind))
        };
        var includeLinks = string.IsNullOrWhiteSpace(kind) ||
            string.Equals(kind, "Link", StringComparison.OrdinalIgnoreCase);
        var assets = new List<MediaAsset>();
        if (!string.Equals(kind, "Link", StringComparison.OrdinalIgnoreCase))
        {
            assets = await _context.MediaAssets
                .Where(asset => asset.ConversationId == conversationId &&
                    (string.IsNullOrWhiteSpace(kind) || asset.Kind == assetKind))
                .OrderByDescending(asset => asset.CreatedAt)
                .Take(200)
                .ToListAsync();
        }
        var result = assets.Select(ToDto).ToList();

        if (includeLinks)
        {
            var messages = await _context.Messages
                .Where(message => message.ConversationId == conversationId)
                .OrderByDescending(message => message.CreatedAt)
                .Take(500)
                .Select(message => new { message.Id, message.SenderId, message.Content, message.CreatedAt })
                .ToListAsync();
            result.AddRange(messages.SelectMany(message => LinkRegex.Matches(message.Content)
                .Select(match => match.Value.TrimEnd('.', ',', ')', ']', '!', '?'))
                .Where(IsSafeHttpUrl)
                .Select(url => new ConversationMediaDto
                {
                    Id = message.Id,
                    Kind = "Link",
                    FileName = string.Empty,
                    UploadedById = message.SenderId,
                    CreatedAt = message.CreatedAt,
                    LinkUrl = url
                })));
        }

        return result.OrderByDescending(item => item.CreatedAt).Take(200).ToList();
    }

    public async Task<(string FileName, long Size)?> GetDownloadInfoAsync(
        Guid userId,
        Guid conversationId,
        Guid fileId)
    {
        if (!await IsMemberAsync(userId, conversationId))
            return null;

        var asset = await _context.MediaAssets.FirstOrDefaultAsync(candidate =>
            candidate.Id == fileId && candidate.ConversationId == conversationId);
        if (asset is null)
            return null;

        return (asset.FileName, asset.Size);
    }

    public async Task DownloadAsync(
        Guid userId,
        Guid conversationId,
        Guid fileId,
        Stream destination,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, conversationId))
            throw new InvalidOperationException("Bạn không thuộc cuộc hội thoại này.");

        var objectKey = await _context.MediaAssets
            .Where(asset => asset.Id == fileId && asset.ConversationId == conversationId)
            .Select(asset => asset.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (objectKey is null)
            throw new FileNotFoundException("Không tìm thấy tệp.");

        await _fileStorage.DownloadAsync(objectKey, destination, cancellationToken);
    }

    private Task<bool> IsMemberAsync(Guid userId, Guid conversationId) =>
        _context.ConversationMembers.AnyAsync(member =>
            member.UserId == userId && member.ConversationId == conversationId);

    private static (MediaKind Kind, string ContentType, long SizeLimit) GetFileType(string extension)
    {
        if (ImageExtensions.Contains(extension))
            return (MediaKind.Image, extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => throw new ConversationFileValidationException("Định dạng ảnh không được hỗ trợ.")
            }, MaxImageBytes);
        if (extension == ".mp4")
            return (MediaKind.File, "video/mp4", MaxVideoBytes);
        if (DocumentExtensions.Contains(extension))
            return (MediaKind.File, extension switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => throw new ConversationFileValidationException("Định dạng tài liệu không được hỗ trợ.")
            }, MaxDocumentBytes);

        throw new ConversationFileValidationException("Định dạng tệp không được hỗ trợ.");
    }

    private static async Task<bool> HasValidSignatureAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[16];
        var bytesRead = await stream.ReadAsync(header, cancellationToken);
        return extension switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71 &&
                header[4] == 13 && header[5] == 10 && header[6] == 26 && header[7] == 10,
            ".gif" => bytesRead >= 6 && (Encoding.ASCII.GetString(header, 0, 6) is "GIF87a" or "GIF89a"),
            ".webp" => bytesRead >= 12 &&
                Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            ".mp4" => bytesRead >= 12 && Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
            ".pdf" => bytesRead >= 5 && Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",
            ".docx" or ".xlsx" or ".pptx" =>
                bytesRead >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
            ".txt" => bytesRead > 0 && !header.AsSpan(0, bytesRead).Contains((byte)0),
            _ => false
        };
    }

    private static bool IsSafeHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static ConversationMediaDto ToDto(MediaAsset asset) => new()
    {
        Id = asset.Id,
        Kind = asset.Kind.ToString(),
        FileName = asset.FileName,
        Size = asset.Size,
        UploadedById = asset.UploadedById,
        CreatedAt = asset.CreatedAt,
        DownloadUrl = $"/api/conversations/{asset.ConversationId}/files/{asset.Id}"
    };
}
