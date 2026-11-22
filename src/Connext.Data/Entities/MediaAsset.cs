namespace Connext.Data.Entities;

public enum MediaKind
{
    Image,
    File
}

public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public MediaKind Kind { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
