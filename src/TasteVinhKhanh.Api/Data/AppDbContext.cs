using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Data;

/// <summary>
/// Tài khoản admin — kế thừa IdentityUser để có sẵn Email, Password, Role...
/// </summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// EF Core DbContext kết nối SQL Server.
/// Kế thừa IdentityDbContext để tự động có các bảng: AspNetUsers, AspNetRoles...
/// </summary>
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PoiPoint> PoiPoints => Set<PoiPoint>();
    public DbSet<AudioScript> AudioScripts => Set<AudioScript>();
    public DbSet<PlaybackLog> PlaybackLogs => Set<PlaybackLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── PoiPoint ──────────────────────────────────────────────────────────
        builder.Entity<PoiPoint>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.ShortDescription).HasMaxLength(500);
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.MapUrl).HasMaxLength(500);

            e.HasMany(p => p.AudioScripts)
             .WithOne(a => a.PoiPoint)
             .HasForeignKey(a => a.PoiPointId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AudioScript ───────────────────────────────────────────────────────
        builder.Entity<AudioScript>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.LanguageCode).IsRequired().HasMaxLength(10);
            e.Property(a => a.TtsScript).HasMaxLength(2000);
            e.Property(a => a.AudioFileUrl).HasMaxLength(500);
            e.HasIndex(a => new { a.PoiPointId, a.LanguageCode }).IsUnique();
        });

        // ── PlaybackLog ───────────────────────────────────────────────────────
        builder.Entity<PlaybackLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.LanguageCode).HasMaxLength(10);
            e.Property(l => l.TriggerType).HasMaxLength(50);
            e.Property(l => l.AnonymousDeviceId).HasMaxLength(100);
            e.HasIndex(l => l.PlayedAt);
            e.HasIndex(l => l.PoiPointId);
            e.HasOne(l => l.PoiPoint)
             .WithMany()
             .HasForeignKey(l => l.PoiPointId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}