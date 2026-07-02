using Microsoft.EntityFrameworkCore;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripMember> TripMembers => Set<TripMember>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<TripEvent> TripEvents => Set<TripEvent>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TripMember composite PK
        modelBuilder.Entity<TripMember>(e =>
        {
            e.HasKey(m => new { m.TripId, m.UserId });
            e.HasOne(m => m.Trip).WithMany(t => t.Members).HasForeignKey(m => m.TripId);
            e.HasOne(m => m.User).WithMany(u => u.TripMemberships).HasForeignKey(m => m.UserId);
        });

        // Friendship composite PK (UserAId < UserBId always)
        modelBuilder.Entity<Friendship>(e =>
        {
            e.HasKey(f => new { f.UserAId, f.UserBId });

            e.HasOne(f => f.UserA)
                .WithMany(u => u.FriendshipsInitiated)
                .HasForeignKey(f => f.UserAId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(f => f.UserB)
                .WithMany(u => u.FriendshipsReceived)
                .HasForeignKey(f => f.UserBId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(f => f.RequestedBy)
                .WithMany()
                .HasForeignKey(f => f.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(f => f.Status)
                .HasConversion<string>();
        });

        // User unique indexes
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Handle).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        // Expense.Share stored as JSON, SplitType as int
        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Share).HasColumnType("jsonb");
            e.Property(x => x.SplitType).HasConversion<int>();
        });

        // Trip → Owner
        modelBuilder.Entity<Trip>(e =>
        {
            e.HasOne(t => t.Owner)
                .WithMany()
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Guest → Trip
        modelBuilder.Entity<Guest>(e =>
        {
            e.HasOne(g => g.Trip)
                .WithMany(t => t.Guests)
                .HasForeignKey(g => g.TripId);
        });

        // Expense → Trip
        modelBuilder.Entity<Expense>(e =>
        {
            e.HasOne(x => x.Trip)
                .WithMany(t => t.Expenses)
                .HasForeignKey(x => x.TripId);
        });

        // TripEvent → Trip
        modelBuilder.Entity<TripEvent>(e =>
        {
            e.HasOne(ev => ev.Trip)
                .WithMany(t => t.Events)
                .HasForeignKey(ev => ev.TripId);
        });

        // RefreshToken → User
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId);
        });

        // PushSubscription → User
        modelBuilder.Entity<PushSubscription>(e =>
        {
            e.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId);
            e.HasIndex(p => new { p.UserId, p.Endpoint }).IsUnique();
        });
    }
}
