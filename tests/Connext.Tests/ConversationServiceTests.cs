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
    public async Task RemoveMemberAsync_RemovesMemberFromActiveConversationConnections()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var thirdFriend = CreateUser("Third");
        context.Users.AddRange(creator, firstFriend, secondFriend, thirdFriend);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, thirdFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id, thirdFriend.Id]
        });

        await service.RemoveMemberAsync(creator.Id, group.Id, firstFriend.Id);

        Assert.Equal((group.Id, firstFriend.Id), Assert.Single(notifier.RemovedMembers));
    }

    [Fact]
    public async Task LeaveGroupAsync_RemovesLeavingMemberFromActiveConversationConnections()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var thirdFriend = CreateUser("Third");
        context.Users.AddRange(creator, firstFriend, secondFriend, thirdFriend);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, thirdFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id, thirdFriend.Id]
        });

        await service.LeaveGroupAsync(firstFriend.Id, group.Id);

        Assert.Equal((group.Id, firstFriend.Id), Assert.Single(notifier.RemovedMembers));
        Assert.True(await context.Conversations.AnyAsync(conversation => conversation.Id == group.Id));
    }

    [Fact]
    public async Task AddMemberAsync_NotifiesAndJoinsTheNewMember()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var newFriend = CreateUser("New");
        context.Users.AddRange(creator, firstFriend, secondFriend, newFriend);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, newFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await service.AddMemberAsync(creator.Id, group.Id, newFriend.Id);

        Assert.Equal(newFriend.Id, Assert.Single(notifier.AddedMemberIds));
        Assert.Contains(newFriend.Id, notifier.NotifiedUserIds);
        Assert.Equal(4, await context.ConversationMembers.CountAsync(member => member.ConversationId == group.Id));
    }

    [Fact]
    public async Task AddMemberAsync_RejectsNonAdminMembers()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var newFriend = CreateUser("New");
        context.Users.AddRange(creator, firstFriend, secondFriend, newFriend);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, newFriend));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddMemberAsync(firstFriend.Id, group.Id, newFriend.Id));

        Assert.False(await context.ConversationMembers.AnyAsync(member =>
            member.ConversationId == group.Id && member.UserId == newFriend.Id));
    }

    [Fact]
    public async Task AddMemberAsync_RejectsNonFriendsAndBlockedFriends()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var newFriend = CreateUser("New");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(creator, firstFriend, secondFriend, newFriend, outsider);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, newFriend));
        context.UserBlocks.Add(new UserBlock { BlockerId = creator.Id, BlockedUserId = newFriend.Id });
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddMemberAsync(creator.Id, group.Id, outsider.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddMemberAsync(creator.Id, group.Id, newFriend.Id));
        Assert.Equal(3, await context.ConversationMembers.CountAsync(member =>
            member.ConversationId == group.Id));
    }

    [Fact]
    public async Task RemoveMemberAsync_RejectsNonAdminsAndOutsiders()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var thirdFriend = CreateUser("Third");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(creator, firstFriend, secondFriend, thirdFriend, outsider);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, thirdFriend));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id, thirdFriend.Id]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveMemberAsync(firstFriend.Id, group.Id, secondFriend.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveMemberAsync(outsider.Id, group.Id, secondFriend.Id));
        Assert.True(await context.ConversationMembers.AnyAsync(member =>
            member.ConversationId == group.Id && member.UserId == secondFriend.Id));
    }

    [Fact]
    public async Task RenameGroupAsync_NotifiesMembersOfTheNewName()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        context.Users.AddRange(creator, firstFriend, secondFriend);
        context.Friendships.AddRange(CreateFriendship(creator, firstFriend), CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await service.RenameGroupAsync(creator.Id, group.Id, "Renamed team");

        Assert.Equal((group.Id, "Renamed team"), Assert.Single(notifier.RenamedConversations));
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_PreventsDemotingTheLastAdmin()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        context.Users.AddRange(creator, firstFriend, secondFriend);
        context.Friendships.AddRange(CreateFriendship(creator, firstFriend), CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangeMemberRoleAsync(creator.Id, group.Id, creator.Id, "Member"));

        Assert.Equal(
            ConversationMemberRole.Admin,
            (await context.ConversationMembers.SingleAsync(member =>
                member.ConversationId == group.Id && member.UserId == creator.Id)).Role);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_NotifiesMembersOfRoleChange()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        context.Users.AddRange(creator, firstFriend, secondFriend);
        context.Friendships.AddRange(CreateFriendship(creator, firstFriend), CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await service.ChangeMemberRoleAsync(creator.Id, group.Id, firstFriend.Id, "Admin");

        Assert.Equal((group.Id, firstFriend.Id, "Admin"), Assert.Single(notifier.RoleChanges));
    }

    [Fact]
    public async Task SetGroupAvatarAsync_RequiresAdminAndNotifiesMembers()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        context.Users.AddRange(creator, firstFriend, secondFriend);
        context.Friendships.AddRange(CreateFriendship(creator, firstFriend), CreateFriendship(creator, secondFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetGroupAvatarAsync(firstFriend.Id, group.Id, "https://example.test/avatar.png"));
        await service.SetGroupAvatarAsync(creator.Id, group.Id, " https://example.test/avatar.png ");

        Assert.Equal("https://example.test/avatar.png",
            (await context.Conversations.SingleAsync(item => item.Id == group.Id)).AvatarUrl);
        Assert.Equal((group.Id, "https://example.test/avatar.png"), Assert.Single(notifier.UpdatedGroupAvatars));
    }

    [Fact]
    public async Task LeaveGroupAsync_TransfersLastAdminRoleWhenCreatorLeaves()
    {
        using var context = CreateDbContext();
        var creator = CreateUser("Creator");
        var firstFriend = CreateUser("First");
        var secondFriend = CreateUser("Second");
        var thirdFriend = CreateUser("Third");
        context.Users.AddRange(creator, firstFriend, secondFriend, thirdFriend);
        context.Friendships.AddRange(
            CreateFriendship(creator, firstFriend),
            CreateFriendship(creator, secondFriend),
            CreateFriendship(creator, thirdFriend));
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);
        var group = await service.CreateGroupAsync(creator.Id, new CreateGroupConversationDto
        {
            Name = "Team",
            MemberIds = [firstFriend.Id, secondFriend.Id, thirdFriend.Id]
        });

        await service.LeaveGroupAsync(creator.Id, group.Id);

        var successorId = new[] { firstFriend.Id, secondFriend.Id, thirdFriend.Id }.Min();
        Assert.False(await context.ConversationMembers.AnyAsync(member =>
            member.ConversationId == group.Id && member.UserId == creator.Id));
        Assert.Equal(ConversationMemberRole.Admin,
            (await context.ConversationMembers.SingleAsync(member =>
                member.ConversationId == group.Id && member.UserId == successorId)).Role);
        Assert.Contains((group.Id, successorId, "Admin"), notifier.RoleChanges);
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
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendMessageAsync(
            sender.Id,
            new SendMessageDto { ConversationId = conversation.Id, Content = "Call type", Type = MessageType.Call }));
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
    public async Task DirectConversationAndMessageFeatures_WorkTogether()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var recipient = CreateUser("Recipient");
        context.Users.AddRange(sender, recipient);
        context.Friendships.Add(CreateFriendship(sender, recipient));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        var direct = await service.CreateDirectAsync(sender.Id, recipient.Id);
        var duplicate = await service.CreateDirectAsync(recipient.Id, sender.Id);

        Assert.Equal(direct.Id, duplicate.Id);
        Assert.False(direct.IsGroup);
        Assert.Equal(2, await context.ConversationMembers.CountAsync(member => member.ConversationId == direct.Id));

        var original = await service.SendMessageAsync(sender.Id, new SendMessageDto
        {
            ConversationId = direct.Id,
            Content = "Tin nhắn gốc",
            MentionedUserIds = [recipient.Id]
        });
        var reply = await service.SendMessageAsync(recipient.Id, new SendMessageDto
        {
            ConversationId = direct.Id,
            Content = "Đã trả lời",
            ReplyToMessageId = original.Id
        });
        Assert.Equal(original.Id, reply.ReplyToMessage!.Id);
        Assert.Equal("Tin nhắn gốc", reply.ReplyToMessage.Content);
        await service.AddReactionAsync(recipient.Id, direct.Id, original.Id, "👍");
        await service.PinMessageAsync(sender.Id, direct.Id, original.Id);

        var messages = await service.GetMessagesAsync(sender.Id, direct.Id, 50);
        Assert.Equal(original.Id, messages.First().Id);
        Assert.Contains(recipient.Id, messages.First().Reactions["👍"]);
        Assert.Contains(recipient.Id, messages.First().MentionedUserIds);

        await service.RecallMessageAsync(sender.Id, direct.Id, original.Id);
        var recalled = await service.GetMessagesAsync(sender.Id, direct.Id, 50);
        Assert.True(recalled.First().IsDeleted);
    }

    [Fact]
    public async Task CreateDirectAsync_RejectsOutsidersAndBlockedFriends()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var friend = CreateUser("Friend");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(sender, friend, outsider);
        context.Friendships.Add(CreateFriendship(sender, friend));
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDirectAsync(sender.Id, outsider.Id));
        context.UserBlocks.Add(new UserBlock { BlockerId = friend.Id, BlockedUserId = sender.Id });
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDirectAsync(sender.Id, friend.Id));
        Assert.Empty(await context.Conversations.ToListAsync());
    }

    [Fact]
    public async Task AddReactionAsync_ReplacesExistingReactionAndNotifiesMembers()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var recipient = CreateUser("Recipient");
        context.Users.AddRange(sender, recipient);
        var conversation = new Conversation { Id = Guid.NewGuid(), CreatedById = sender.Id };
        context.Conversations.Add(conversation);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversation.Id, UserId = sender.Id },
            new ConversationMember { ConversationId = conversation.Id, UserId = recipient.Id });
        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = sender.Id,
            Content = "Hello",
            Type = MessageType.Text
        };
        context.Messages.Add(message);
        context.MessageReactions.Add(new MessageReaction
        {
            MessageId = message.Id,
            UserId = recipient.Id,
            Emoji = "👍"
        });
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);

        await service.AddReactionAsync(recipient.Id, conversation.Id, message.Id, "❤️");

        var reaction = await context.MessageReactions.SingleAsync();
        Assert.Equal("❤️", reaction.Emoji);
        Assert.Equal((conversation.Id, message.Id, recipient.Id, "👍"), Assert.Single(notifier.RemovedReactions));
        Assert.Equal((conversation.Id, message.Id, recipient.Id, "❤️"), Assert.Single(notifier.AddedReactions));
    }

    [Fact]
    public async Task RecallAndRemoveReactionAsync_EnforceConversationObjectAuthorization()
    {
        using var context = CreateDbContext();
        var sender = CreateUser("Sender");
        var member = CreateUser("Member");
        var outsider = CreateUser("Outsider");
        context.Users.AddRange(sender, member, outsider);
        var firstConversation = new Conversation { Id = Guid.NewGuid(), CreatedById = sender.Id };
        var secondConversation = new Conversation { Id = Guid.NewGuid(), CreatedById = sender.Id };
        context.Conversations.AddRange(firstConversation, secondConversation);
        context.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = firstConversation.Id, UserId = sender.Id },
            new ConversationMember { ConversationId = firstConversation.Id, UserId = member.Id },
            new ConversationMember { ConversationId = secondConversation.Id, UserId = sender.Id },
            new ConversationMember { ConversationId = secondConversation.Id, UserId = outsider.Id });
        var message = new Message
        {
            ConversationId = secondConversation.Id,
            SenderId = outsider.Id,
            Content = "Private",
            Type = MessageType.Text
        };
        context.Messages.Add(message);
        context.MessageReactions.Add(new MessageReaction
        {
            MessageId = message.Id,
            UserId = member.Id,
            Emoji = "👍"
        });
        await context.SaveChangesAsync();
        var service = new ConversationService(context, new TestChatNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecallMessageAsync(member.Id, firstConversation.Id, message.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveReactionAsync(member.Id, firstConversation.Id, message.Id, "👍"));

        Assert.False((await context.Messages.SingleAsync()).IsDeleted);
        Assert.Single(await context.MessageReactions.ToListAsync());
    }

    [Fact]
    public async Task PinMessageAsync_NotifiesAndRejectsNonMembers()
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
        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = sender.Id,
            Content = "Hello",
            Type = MessageType.Text
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();
        var notifier = new TestChatNotifier();
        var service = new ConversationService(context, notifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PinMessageAsync(outsider.Id, conversation.Id, message.Id));
        await service.PinMessageAsync(sender.Id, conversation.Id, message.Id);
        Assert.Single(await service.GetPinnedMessagesAsync(recipient.Id, conversation.Id));
        await service.UnpinMessageAsync(sender.Id, conversation.Id, message.Id);
        Assert.Empty(await service.GetPinnedMessagesAsync(recipient.Id, conversation.Id));

        Assert.Equal((conversation.Id, message.Id, sender.Id), Assert.Single(notifier.PinnedMessages));
        Assert.Equal((conversation.Id, message.Id, sender.Id), Assert.Single(notifier.UnpinnedMessages));
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
        public List<Guid> AddedMemberIds { get; } = [];
        public List<(Guid ConversationId, Guid UserId)> RemovedMembers { get; } = [];
        public List<(Guid ConversationId, string Name)> RenamedConversations { get; } = [];
        public List<(Guid ConversationId, Guid MemberId, string Role)> RoleChanges { get; } = [];
        public List<(Guid ConversationId, string? AvatarUrl)> UpdatedGroupAvatars { get; } = [];
        public List<(Guid ConversationId, Guid MessageId, Guid UserId, string Emoji)> AddedReactions { get; } = [];
        public List<(Guid ConversationId, Guid MessageId, Guid UserId, string Emoji)> RemovedReactions { get; } = [];
        public List<(Guid ConversationId, Guid MessageId, Guid UserId)> PinnedMessages { get; } = [];
        public List<(Guid ConversationId, Guid MessageId, Guid UserId)> UnpinnedMessages { get; } = [];

        public Task ConversationCreatedAsync(Guid userId, ConversationSummaryDto conversation)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task ConversationMemberAddedAsync(Guid userId, ConversationSummaryDto conversation)
        {
            AddedMemberIds.Add(userId);
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task ConversationMembersRemovedAsync(
            Guid conversationId,
            IReadOnlyCollection<Guid> removedUserIds,
            IReadOnlyCollection<Guid> remainingUserIds)
        {
            RemovedMembers.AddRange(removedUserIds.Select(userId => (conversationId, userId)));
            return Task.CompletedTask;
        }

        public Task ConversationRenamedAsync(Guid conversationId, string name)
        {
            RenamedConversations.Add((conversationId, name));
            return Task.CompletedTask;
        }

        public Task ConversationMemberRoleChangedAsync(Guid conversationId, Guid memberId, string role)
        {
            RoleChanges.Add((conversationId, memberId, role));
            return Task.CompletedTask;
        }

        public Task GroupAvatarUpdatedAsync(Guid conversationId, string? avatarUrl)
        {
            UpdatedGroupAvatars.Add((conversationId, avatarUrl));
            return Task.CompletedTask;
        }

        public Task MessageReactionAddedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji)
        {
            AddedReactions.Add((conversationId, messageId, userId, emoji));
            return Task.CompletedTask;
        }

        public Task MessageReactionRemovedAsync(Guid conversationId, Guid messageId, Guid userId, string emoji)
        {
            RemovedReactions.Add((conversationId, messageId, userId, emoji));
            return Task.CompletedTask;
        }

        public Task MessagePinnedAsync(Guid conversationId, Guid messageId, Guid userId)
        {
            PinnedMessages.Add((conversationId, messageId, userId));
            return Task.CompletedTask;
        }

        public Task MessageUnpinnedAsync(Guid conversationId, Guid messageId, Guid userId)
        {
            UnpinnedMessages.Add((conversationId, messageId, userId));
            return Task.CompletedTask;
        }

        public Task CallHistoryRecordedAsync(Guid conversationId, MessageResponseDto message) =>
            Task.CompletedTask;
    }
}
