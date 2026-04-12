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
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourStop> TourStops => Set<TourStop>();
    public DbSet<RestaurantImage> RestaurantImages => Set<RestaurantImage>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PendingPOIUpdate> PendingPOIUpdates => Set<PendingPOIUpdate>();
    public DbSet<StagingImage> StagingImages => Set<StagingImage>();

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
            e.Property(p => p.IconUrl).HasMaxLength(500);
            e.Property(p => p.MapUrl).HasMaxLength(500);
            e.Property(p => p.LogoUrl).HasMaxLength(500);

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
            // Dùng NVARCHAR (mặc định trong SQL Server .NET provider)
            // để lưu đúng CJK characters (tiếng Trung, Hàn, Nhật)
            e.Property(a => a.TtsScript).HasMaxLength(4000);
            e.Property(a => a.AudioFilePath).HasMaxLength(500);
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

        // ── Tour ──────────────────────────────────────────────────────────────
        builder.Entity<Tour>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Description).HasMaxLength(1000);
            e.Property(t => t.CreatedBy).IsRequired().HasMaxLength(256);

            e.HasMany(t => t.TourStops)
             .WithOne(s => s.Tour)
             .HasForeignKey(s => s.TourId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TourStop ──────────────────────────────────────────────────────────
        builder.Entity<TourStop>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.TourId, s.PoiPointId }).IsUnique();
            e.HasIndex(s => new { s.TourId, s.StopOrder });

            e.HasOne(s => s.PoiPoint)
             .WithMany()
             .HasForeignKey(s => s.PoiPointId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RestaurantImage ───────────────────────────────────────────────────
        builder.Entity<RestaurantImage>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.ImageUrl).IsRequired().HasMaxLength(500);
            e.Property(i => i.IsPrimary).HasDefaultValue(false);
            e.Property(i => i.SortOrder).HasDefaultValue(0);

            // Mỗi quán chỉ có 1 ảnh chính
            e.HasIndex(i => new { i.PoiPointId, i.IsPrimary })
             .HasFilter("[IsPrimary] = 1")
             .IsUnique();

            e.HasOne(i => i.PoiPoint)
             .WithMany(p => p.Images)
             .HasForeignKey(i => i.PoiPointId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Vendor ────────────────────────────────────────────────────────────
        builder.Entity<Vendor>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.BusinessName).IsRequired().HasMaxLength(200);
            e.Property(v => v.OwnerName).IsRequired().HasMaxLength(100);
            e.Property(v => v.Phone).IsRequired().HasMaxLength(20);
            e.Property(v => v.Address).HasMaxLength(500);
            e.Property(v => v.Status).IsRequired().HasMaxLength(20);
            e.Property(v => v.RejectedReason).HasMaxLength(500);

            // 1 UserId chỉ có 1 Vendor
            e.HasIndex(v => v.UserId).IsUnique();

            // 1 PoiPoint chỉ có 1 Vendor (null nếu chưa gán)
            e.HasIndex(v => v.PoiPointId);

            e.HasOne(v => v.PoiPoint)
             .WithMany()
             .HasForeignKey(v => v.PoiPointId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── PendingPOIUpdate ─────────────────────────────────────────────────
        builder.Entity<PendingPOIUpdate>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Status).IsRequired().HasMaxLength(20);
            e.Property(u => u.AdminNote).HasMaxLength(500);
            e.Property(u => u.ReviewedBy).HasMaxLength(256);

            e.HasIndex(u => u.Status);
            e.HasIndex(u => u.VendorId);
            e.HasIndex(u => u.PoiPointId);

            e.HasOne(u => u.Vendor)
             .WithMany()
             .HasForeignKey(u => u.VendorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── StagingImage ───────────────────────────────────────────────────
        builder.Entity<StagingImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(255);
            e.Property(x => x.TempUrl).IsRequired().HasMaxLength(500);
            e.Property(x => x.ApprovedUrl).HasMaxLength(500);
            e.Property(x => x.StagingType).IsRequired().HasMaxLength(20).HasDefaultValue("Upload");
            e.Property(x => x.ReferencedImageUrl).HasMaxLength(500);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.AdminNote).HasMaxLength(500);
            e.Property(x => x.ReviewedBy).HasMaxLength(256);

            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.VendorId);
            e.HasIndex(x => x.PoiPointId);

            e.HasOne(x => x.Vendor)
             .WithMany()
             .HasForeignKey(x => x.VendorId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.PoiPoint)
             .WithMany()
             .HasForeignKey(x => x.PoiPointId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}