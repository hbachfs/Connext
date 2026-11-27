using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Connext.Tests;

public class PollServiceTests
{
    [Fact]
    public async Task PollCreationAndVoting_ReturnResultsAndNotifyMembers()
    {
        using var context = CreateContext();
        var creator = CreateUser();
        var member = CreateUser();
        var outsider = CreateUser();
        var group = CreateConversation(creator.Id, isGroup: true);
        context.Users.AddRange(creator, member, outsider);
        context.Conversations.Add(group);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = group.Id, UserId = creator.Id },
            new ConversationMember { ConversationId = group.Id, UserId = member.Id });
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);

        var created = await service.CreatePollAsync(creator.Id, group.Id, new CreatePollDto
        {
            Question = "Where should we meet?",
            Options = ["Cafe", "Library"]
        });

        Assert.Equal(0, created.TotalVotes);
        Assert.Equal(2, created.Options.Count);
        Assert.All(created.Options, option =>
        {
            Assert.Equal(0, option.VoteCount);
            Assert.Equal(0m, option.Percentage);
        });
        var createdEvent = Assert.Single(notifier.CreatedPolls);
        Assert.Equal(group.Id, createdEvent.ConversationId);
        Assert.Equal(MessageType.Poll, createdEvent.Message.Type);
        Assert.Equal(created.Id, createdEvent.Message.Poll!.Id);

        var message = Assert.Single(await service.GetMessagesAsync(creator.Id, group.Id, 50));
        Assert.Equal(MessageType.Poll, message.Type);
        Assert.Equal(created.Id, message.Poll!.Id);

        var firstOption = created.Options[0];
        var secondOption = created.Options[1];
        var firstVote = await service.VotePollAsync(creator.Id, group.Id, created.Id, firstOption.Id);
        Assert.Equal(firstOption.Id, firstVote.MyVoteOptionId);
        Assert.Equal(100m, firstVote.Options[0].Percentage);
        Assert.Null(Assert.Single(notifier.VotedPolls).Update.Poll.MyVoteOptionId);
        Assert.Equal(creator.Id, notifier.VotedPolls[0].Update.UserId);
        Assert.Equal(firstOption.Id, notifier.VotedPolls[0].Update.OptionId);

        var twoVotes = await service.VotePollAsync(member.Id, group.Id, created.Id, secondOption.Id);
        Assert.Equal(2, twoVotes.TotalVotes);
        Assert.Equal(50m, twoVotes.Options[0].Percentage);
        Assert.Equal(50m, twoVotes.Options[1].Percentage);

        var changedVote = await service.VotePollAsync(creator.Id, group.Id, created.Id, secondOption.Id);
        Assert.Equal(secondOption.Id, changedVote.MyVoteOptionId);
        Assert.Equal(2, changedVote.TotalVotes);
        Assert.Equal(0, changedVote.Options[0].VoteCount);
        Assert.Equal(2, changedVote.Options[1].VoteCount);
        Assert.Equal(100m, changedVote.Options[1].Percentage);
        Assert.Equal(3, notifier.VotedPolls.Count);
        Assert.Equal(2, await context.PollVotes.CountAsync());
        var memberView = await service.GetPollAsync(member.Id, group.Id, created.Id);
        Assert.Equal(secondOption.Id, memberView.MyVoteOptionId);
        var messageView = Assert.Single(await service.GetMessagesAsync(member.Id, group.Id, 50));
        Assert.Equal(2, messageView.Poll!.TotalVotes);
        Assert.Equal(secondOption.Id, messageView.Poll.MyVoteOptionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetPollAsync(outsider.Id, group.Id, created.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VotePollAsync(outsider.Id, group.Id, created.Id, firstOption.Id));
    }

    [Fact]
    public async Task PollCreation_RequiresGroupMembershipAndTwoToTenOptions()
    {
        using var context = CreateContext();
        var creator = CreateUser();
        var outsider = CreateUser();
        var group = CreateConversation(creator.Id, isGroup: true);
        var direct = CreateConversation(creator.Id, isGroup: false);
        context.Users.AddRange(creator, outsider);
        context.Conversations.AddRange(group, direct);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = group.Id, UserId = creator.Id },
            new ConversationMember { ConversationId = direct.Id, UserId = creator.Id });
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePollAsync(
            creator.Id, group.Id, new CreatePollDto { Question = "Q", Options = ["Only one"] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePollAsync(
            outsider.Id, group.Id, new CreatePollDto { Question = "Q", Options = ["A", "B"] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePollAsync(
            creator.Id, direct.Id, new CreatePollDto { Question = "Q", Options = ["A", "B"] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePollAsync(
            creator.Id, group.Id, new CreatePollDto
            {
                Question = new string('q', 201),
                Options = ["A", "B"]
            }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePollAsync(
            creator.Id, group.Id, new CreatePollDto
            {
                Question = "Q",
                Options = Enumerable.Range(0, 11).Select(index => $"Option {index}").ToList()
            }));
    }

    [Fact]
    public async Task VotePollAsync_RejectsAnOptionFromAnotherPoll()
    {
        using var context = CreateContext();
        var creator = CreateUser();
        var group = CreateConversation(creator.Id, isGroup: true);
        context.Users.Add(creator);
        context.Conversations.Add(group);
        context.ConversationMembers.Add(new ConversationMember
        {
            ConversationId = group.Id,
            UserId = creator.Id
        });
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());
        var firstPoll = await service.CreatePollAsync(creator.Id, group.Id, new CreatePollDto
        {
            Question = "First",
            Options = ["A", "B"]
        });
        var secondPoll = await service.CreatePollAsync(creator.Id, group.Id, new CreatePollDto
        {
            Question = "Second",
            Options = ["C", "D"]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VotePollAsync(creator.Id, group.Id, firstPoll.Id, secondPoll.Options[0].Id));
        Assert.Empty(await context.PollVotes.ToListAsync());
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = Guid.NewGuid().ToString("N")[..20],
        DisplayName = "Poll member",
        PasswordHash = "test"
    };

    private static Conversation CreateConversation(Guid creatorId, bool isGroup) => new()
    {
        Id = Guid.NewGuid(),
        CreatedById = creatorId,
        IsGroup = isGroup
    };

    private sealed class TestChatNotifier : IChatNotifier
    {
        public List<(Guid ConversationId, MessageResponseDto Message)> CreatedPolls { get; } = [];
        public List<(Guid ConversationId, PollVoteUpdateDto Update)> VotedPolls { get; } = [];

        public Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation) => Task.CompletedTask;
        public Task ConversationMemberAddedAsync(Guid userId, ConversationSummaryDto conversation) => Task.CompletedTask;
        public Task ConversationMembersRemovedAsync(Guid conversationId, IReadOnlyCollection<Guid> removedUserIds,
            IReadOnlyCollection<Guid> remainingUserIds) => Task.CompletedTask;
        public Task ConversationRenamedAsync(Guid conversationId, string name) => Task.CompletedTask;
        public Task ConversationMemberRoleChangedAsync(Guid conversationId, Guid memberId, string role) => Task.CompletedTask;
        public Task GroupAvatarUpdatedAsync(Guid conversationId, string? avatarUrl) => Task.CompletedTask;
        public Task MessageReactionAddedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji) => Task.CompletedTask;
        public Task MessageReactionRemovedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji) => Task.CompletedTask;
        public Task MessagePinnedAsync(Guid conversationId, Guid messageId, Guid userId) => Task.CompletedTask;
        public Task MessageUnpinnedAsync(Guid conversationId, Guid messageId, Guid userId) => Task.CompletedTask;
        public Task CallHistoryRecordedAsync(Guid conversationId, MessageResponseDto message) => Task.CompletedTask;

        public Task PollCreatedAsync(Guid conversationId, MessageResponseDto message)
        {
            CreatedPolls.Add((conversationId, message));
            return Task.CompletedTask;
        }

        public Task PollVotedAsync(Guid conversationId, PollVoteUpdateDto update)
        {
            VotedPolls.Add((conversationId, update));
            return Task.CompletedTask;
        }
    }
}
