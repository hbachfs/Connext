namespace Connext.Data.Entities;

public class PollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public Poll Poll { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public ICollection<PollVote> Votes { get; set; } = [];
}
