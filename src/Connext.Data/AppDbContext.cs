using Connext.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connext.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageReceipt> MessageReceipts => Set<MessageReceipt>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<PasswordResetGrant> PasswordResetGrants => Set<PasswordResetGrant>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(f => f.Id);

            entity.HasOne(f => f.Requester)
                  .WithMany()
                  .HasForeignKey(f => f.RequesterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Addressee)
                  .WithMany()
                  .HasForeignKey(f => f.AddresseeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(f => new { f.PairUserLowId, f.PairUserHighId }).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.PhoneNumber)
                  .IsUnique();

            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasFilter("[Email] IS NOT NULL");

            entity.Property(u => u.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(block => new { block.BlockerId, block.BlockedUserId });
            entity.HasOne(block => block.Blocker).WithMany().HasForeignKey(block => block.BlockerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(block => block.BlockedUser).WithMany().HasForeignKey(block => block.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.Property(conversation => conversation.Name).HasMaxLength(100);
            entity.HasOne(conversation => conversation.CreatedBy).WithMany().HasForeignKey(conversation => conversation.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(conversation => conversation.CreatedAt);
        });

        modelBuilder.Entity<ConversationMember>(entity =>
        {
            entity.HasKey(member => new { member.ConversationId, member.UserId });
            entity.HasOne(member => member.Conversation).WithMany(conversation => conversation.Members)
                .HasForeignKey(member => member.ConversationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(member => member.User).WithMany().HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(member => member.UserId);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Content).HasMaxLength(4000).IsRequired();
            entity.HasOne(message => message.Conversation).WithMany(conversation => conversation.Messages)
                .HasForeignKey(message => message.ConversationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(message => message.Sender).WithMany().HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAt });
        });

        modelBuilder.Entity<MessageReceipt>(entity =>
        {
            entity.HasKey(receipt => new { receipt.MessageId, receipt.UserId });
            entity.HasOne(receipt => receipt.Message).WithMany(message => message.Receipts)
                .HasForeignKey(receipt => receipt.MessageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(receipt => receipt.User).WithMany().HasForeignKey(receipt => receipt.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.HasKey(otp => otp.Id);
            entity.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasOne(otp => otp.User).WithMany().HasForeignKey(otp => otp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(otp => new { otp.UserId, otp.CreatedAt });
        });

        modelBuilder.Entity<PasswordResetGrant>(entity =>
        {
            entity.HasKey(grant => grant.TokenId);
            entity.Property(grant => grant.TokenId).HasMaxLength(64);
            entity.HasOne(grant => grant.User).WithMany().HasForeignKey(grant => grant.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.HasKey(asset => asset.Id);
            entity.Property(asset => asset.ObjectKey).HasMaxLength(200).IsRequired();
            entity.Property(asset => asset.FileName).HasMaxLength(255).IsRequired();
            entity.Property(asset => asset.Extension).HasMaxLength(16).IsRequired();
            entity.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
            entity.HasIndex(asset => new { asset.ConversationId, asset.Kind, asset.CreatedAt });
            entity.HasOne(asset => asset.Conversation).WithMany().HasForeignKey(asset => asset.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(asset => asset.UploadedBy).WithMany().HasForeignKey(asset => asset.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
