using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SecretSanta.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Exclusion> Exclusions => Set<Exclusion>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Game>(e =>
        {
            e.Property(x => x.Budget).HasMaxLength(Limits.Budget);
            e.Property(x => x.Note).HasMaxLength(Limits.Note);
            e.HasIndex(x => x.AdminToken).IsUnique();
            e.HasIndex(x => x.InviteToken).IsUnique();
            e.HasMany(x => x.Participants).WithOne(x => x.Game).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Exclusions).WithOne().HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Participant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(Limits.PersonName);
            e.Property(x => x.Login).HasMaxLength(Limits.Login);
            e.HasIndex(x => new { x.GameId, x.Login }).IsUnique();
            e.Property(x => x.Wishlist).HasMaxLength(Limits.Wishlist);
            e.Property(x => x.NotWanted).HasMaxLength(Limits.NotWanted);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.ResetToken).IsUnique();
            e.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Message>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(Limits.Message);
            e.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Exclusion>(e =>
        {
            e.HasOne<Participant>().WithMany().HasForeignKey(x => x.FirstId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Participant>().WithMany().HasForeignKey(x => x.SecondId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PushSubscription>(e =>
        {
            e.Property(x => x.Endpoint).HasMaxLength(Limits.PushEndpoint);
            e.Property(x => x.P256dh).HasMaxLength(100);
            e.Property(x => x.Auth).HasMaxLength(50);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
        });
    }
}

public static class Limits
{
    public const int Budget = 50;
    public const int Note = 1000;
    public const int PersonName = 80;
    public const int Wishlist = 2000;
    public const int NotWanted = 1000;
    public const int Message = 1000;
    public const int LoginMin = 3;
    public const int Login = 32;
    public const int PasswordMin = 6;
    public const int Password = 100;
    public const int PushEndpoint = 2000;
    public const int MaxParticipants = 200;

    public static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}
