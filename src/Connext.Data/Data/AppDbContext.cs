using Connext.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Connext.Shared;
namespace Connext.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationMember> ConversationMembers { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UserBlock> UserBlocks { get; set; }

    public DbSet<MessageMention> MessageMentions { get; set; }

    public DbSet<MessageReaction> MessageReactions { get; set; }
    public DbSet<MessagePin> MessagePins { get; set; }

    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ChatMessage
        builder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.SenderId).IsRequired().HasMaxLength(450);
            e.Property(x => x.ReceiverId).IsRequired().HasMaxLength(450);
            e.Property(x => x.Content).IsRequired().HasMaxLength(2000);
            e.HasIndex(x => new { x.SenderId, x.ReceiverId, x.SentAt });
            e.HasIndex(x => new { x.ReceiverId, x.IsSeen });
        });

        // User Constraints
        builder.Entity<User>().HasIndex(u => u.Phone).IsUnique();
        builder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Friendship Constraints (Ngăn kết bạn trùng)
        builder.Entity<Friendship>()
            .HasIndex(f => new { f.RequesterId, f.AddresseeId })
            .IsUnique();

        builder.Entity<Friendship>()
            .HasOne(f => f.Requester)
            .WithMany(u => u.SentFriendRequests)
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Friendship>()
            .HasOne(f => f.Addressee)
            .WithMany(u => u.ReceivedFriendRequests)
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chat 1-1: mỗi cặp người chỉ có 1 hội thoại (index unique trên cột nullable => SQL Server tự lọc IS NOT NULL)
        builder.Entity<Conversation>()
            .HasIndex(c => c.DirectKey)
            .IsUnique();

        // Conversation Member Constraints
        builder.Entity<ConversationMember>()
            .HasKey(cm => new { cm.ConversationId, cm.UserId });

        builder.Entity<ConversationMember>()
            .HasOne(cm => cm.User)
            .WithMany(u => u.ConversationMembers)
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ConversationMember>()
            .HasOne(cm => cm.Conversation)
            .WithMany(c => c.Members)
            .HasForeignKey(cm => cm.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message Constraints
        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany() // Không cần map ngược lại User
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.Id }); // Tối ưu cho Cursor Pagination

        // UserBlock
        builder.Entity<UserBlock>().HasKey(b => new { b.BlockerId, b.BlockedUserId });
        builder.Entity<UserBlock>().HasOne(b => b.Blocker).WithMany()
            .HasForeignKey(b => b.BlockerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<UserBlock>().HasOne(b => b.BlockedUser).WithMany()
            .HasForeignKey(b => b.BlockedUserId).OnDelete(DeleteBehavior.Restrict);


        // trong OnModelCreating(ModelBuilder builder)
        builder.Entity<MessageMention>().HasKey(x => new { x.MessageId, x.UserId });

        builder.Entity<MessageMention>()
            .HasOne(x => x.Message).WithMany(m => m.Mentions)
            .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessageMention>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageMention>().HasIndex(x => x.UserId);

        builder.Entity<ConversationMember>().Property(x => x.Role).HasMaxLength(20).HasDefaultValue("Member");
        builder.Entity<Conversation>().Property(x => x.Name).HasMaxLength(100);
        //
        builder.Entity<Message>()
    .HasOne(m => m.ReplyToMessage).WithMany()
    .HasForeignKey(m => m.ReplyToMessageId)
    .OnDelete(DeleteBehavior.Restrict);

        // Reaction: 1 người 1 reaction cho 1 tin (thêm = insert, đổi = update, bỏ = delete)
        builder.Entity<MessageReaction>().HasKey(r => new { r.MessageId, r.UserId });
        builder.Entity<MessageReaction>().Property(r => r.Emoji).HasMaxLength(16);
        builder.Entity<MessageReaction>()
            .HasOne(r => r.Message).WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MessageReaction>()
            .HasOne(r => r.User).WithMany()
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

        // Ghim: 1 tin chỉ ghim 1 lần
        builder.Entity<MessagePin>().HasKey(p => p.MessageId);
        builder.Entity<MessagePin>().HasIndex(p => p.ConversationId);
        builder.Entity<MessagePin>()
            .HasOne(p => p.Message).WithMany()
            .HasForeignKey(p => p.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MessagePin>()
            .HasOne(p => p.PinnedBy).WithMany()
            .HasForeignKey(p => p.PinnedById).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Poll>(e =>
        {
            e.Property(p => p.Question).HasMaxLength(Limits.PollQuestionMaxLength).IsRequired();
            e.HasOne(p => p.Message).WithOne(m => m.Poll).HasForeignKey<Poll>(p => p.MessageId);
            e.HasIndex(p => p.MessageId).IsUnique();
        });
        builder.Entity<PollOption>(e =>
        {
            e.Property(o => o.Text).HasMaxLength(Limits.PollOptionMaxLength).IsRequired();
            e.HasOne(o => o.Poll).WithMany(p => p.Options).HasForeignKey(o => o.PollId);
        });
        builder.Entity<PollVote>(e =>
        {
            e.HasIndex(v => new { v.PollId, v.UserId }).IsUnique();   // mỗi người một phiếu
            e.HasOne(v => v.Poll).WithMany(p => p.Votes).HasForeignKey(v => v.PollId);
            // Restrict để tránh lỗi "multiple cascade paths" (Poll -> Options -> Votes và Poll -> Votes)
            e.HasOne(v => v.Option).WithMany().HasForeignKey(v => v.OptionId).OnDelete(DeleteBehavior.Restrict);
        });


    }
}