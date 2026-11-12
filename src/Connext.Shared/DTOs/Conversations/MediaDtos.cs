namespace Connext.Shared.DTOs.Conversations;

public class ConversationMediaDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? LinkUrl { get; set; }
}
