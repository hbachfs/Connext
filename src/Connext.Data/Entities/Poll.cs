namespace Connext.Data.Entities;

public class Poll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long MessageId { get; set; }
    public virtual Message Message { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public string Question { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public virtual List<PollOption> Options { get; set; } = [];
    public virtual List<PollVote> Votes { get; set; } = [];
}

public class PollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public virtual Poll Poll { get; set; } = null!;
    public string Text { get; set; } = null!;
    public int Position { get; set; }
}

public class PollVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public virtual Poll Poll { get; set; } = null!;
    public Guid OptionId { get; set; }
    public virtual PollOption Option { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}