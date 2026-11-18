using Microsoft.EntityFrameworkCore;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(string senderId, string receiverId, string content, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userId, string otherUserId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<int> MarkAsSeenAsync(string receiverId, string senderId, CancellationToken ct = default);
}

public class ChatService : IChatService
{
    private const int MaxContentLength = 2000;
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db) => _db = db;

    public async Task<ChatMessage> SendMessageAsync(string senderId, string receiverId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Nội dung tin nhắn không được để trống.", nameof(content));

        content = content.Trim();
        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Tin nhắn tối đa {MaxContentLength} ký tự.", nameof(content));

        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsSeen = false
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userId, string otherUserId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;

        var items = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId)
                     || (m.SenderId == otherUserId && m.ReceiverId == userId))
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        items.Reverse(); // trả về theo thứ tự cũ -> mới
        return items;
    }

    public async Task<int> MarkAsSeenAsync(string receiverId, string senderId, CancellationToken ct = default)
    {
        var unseen = await _db.ChatMessages
            .Where(m => m.ReceiverId == receiverId && m.SenderId == senderId && !m.IsSeen)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var m in unseen)
        {
            m.IsSeen = true;
            
        }

        await _db.SaveChangesAsync(ct);
        return unseen.Count;
    }
}