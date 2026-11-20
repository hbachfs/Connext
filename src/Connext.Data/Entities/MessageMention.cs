namespace Connext.Data.Entities;

public class MessageMention
{
    public long MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}