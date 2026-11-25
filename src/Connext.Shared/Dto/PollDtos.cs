namespace Connext.Shared.Chat;

public record PollOptionDto
{
    public Guid Id { get; init; }
    public string Text { get; init; } = "";
    public int VoteCount { get; init; }
    public double Percent { get; init; }
}

public record PollDto
{
    public Guid PollId { get; init; }
    public long MessageId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid CreatedById { get; init; }
    public string Question { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int TotalVotes { get; init; }
    public Guid? MyOptionId { get; init; }
    public List<PollOptionDto> Options { get; init; } = [];
}

// Payload của sự kiện PollVoted (MyOptionId = null vì gửi chung cho cả nhóm)
public record PollVotedDto(PollDto Poll, Guid VoterId, Guid OptionId);

public record CreatePollRequest(string? Question, List<string>? Options);
public record VotePollRequest(Guid OptionId);