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

            entity.Property(u => u.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
