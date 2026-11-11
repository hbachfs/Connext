using Connext.Data.Entities;

using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
