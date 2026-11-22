using Connext.Api.Services;
using Connext.Data;
using Connext.Data.Entities;
using Connext.Shared.DTOs.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Connext.Tests;

public class ConversationServiceTests
{
    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateGroupAsync_RequiresThreeMembersAndFriends()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(creator, firstFriend, secondFriend, outsider);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGroupAsync(
            creator.Id,
            new CreateGroupConversationDto { Name = "Too small", MemberIds = [firstFriend.Id] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGroupAsync(
            creator.Id,
            new CreateGroupConversationDto { Name = "Not friends", MemberIds = [firstFriend.Id, outsider.Id] }));

        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        Assert.True(group.IsGroup);
        Assert.Equal("Team", group.Name);
        Assert.Equal(3, await context.ConversationMembers.CountAsync(member => member.ConversationId == group.Id));
    }

    [Fact]
    public async Task GroupCreation_NotifiesEveryMember()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        context.Users.AddRange(creator, firstFriend, secondFriend);
        context.Friendships.AddRange(CreateFriendship(creator, firstFriend), CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();

        await new ConversationService(context, notifier).CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        Assert.Equal(3, notifier.NotifiedUserIds.Count);
        Assert.Contains(creator.Id, notifier.NotifiedUserIds);
        Assert.Contains(firstFriend.Id, notifier.NotifiedUserIds);
        Assert.Contains(secondFriend.Id, notifier.NotifiedUserIds);
    }

    [Fact]
    public async Task SendMessageAsync_ValidatesMembershipAndRecordsActualDelivery()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var recipient = CreateUser("Recipient");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(sender, recipient, outsider);
        var conversation = new Conversation { Id = Guid.NewGuid(), CreatedById = sender.Id };
        context.Conversations.Add(conversation);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversation.Id, UserId = sender.Id },
            new ConversationMember { ConversationId = conversation.Id, UserId = recipient.Id });
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendMessageAsync(
            outsider.Id,
            new SendMessageDto { ConversationId = conversation.Id, Content = "Unauthorized" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendMessageAsync(
            sender.Id,
            new SendMessageDto { ConversationId = conversation.Id, Content = "Invalid type", Type = (MessageType)99 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetMessagesAsync(sender.Id, conversation.Id, 500));

        var sent = await service.SendMessageAsync(sender.Id, new SendMessageDto
        {
            ConversationId = conversation.Id,
            Content = "Hello",
            Type = MessageType.Text
        });
        var messageSender = await service.RecordDeliveredAsync(recipient.Id, sent.Id);
        var messages = await service.GetMessagesAsync(sender.Id, conversation.Id, 50);

        Assert.Equal(sender.Id, messageSender);
        Assert.Equal(recipient.Id, Assert.Single(Assert.Single(messages).DeliveredToUserIds));
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsLastMessageAndUnreadCount()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var recipient = CreateUser("Recipient");
        context.Users.AddRange(sender, recipient);
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CreatedById = sender.Id,
            IsGroup = false
        };
        context.Conversations.Add(conversation);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversation.Id, UserId = sender.Id },
            new ConversationMember { ConversationId = conversation.Id, UserId = recipient.Id });
        context.Messages.Add(new Message
        {
            ConversationId = conversation.Id,
            SenderId = sender.Id,
            Content = "Latest",
            Type = MessageType.Text,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await new ConversationService(context, new TestChatNotifier())
            .GetConversationsAsync(recipient.Id);

        var summary = Assert.Single(result);
        Assert.Equal("Sender", summary.Name);
        Assert.Equal("Latest", summary.LastMessage!.Content);
        Assert.Equal(1, summary.UnreadCount);
    }

    private static User CreateUser(string name) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = Guid.NewGuid().ToString("N")[..20],
        DisplayName = name,
        PasswordHash = "test-hash"
    };

    private static Friendship CreateFriendship(User first, User second)
    {
        var firstIsLow = string.CompareOrdinal(first.Id.ToString("N"), second.Id.ToString("N")) < 0;
        return new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = first.Id,
            AddresseeId = second.Id,
            Requester = first,
            Addressee = second,
            PairUserLowId = firstIsLow ? first.Id : second.Id,
            PairUserHighId = firstIsLow ? second.Id : first.Id,
            Status = FriendshipStatus.Accepted
        };
    }

    private sealed class TestChatNotifier : IChatNotifier
    {
        public List<Guid> NotifiedUserIds { get; } = [];

        public Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }
}
