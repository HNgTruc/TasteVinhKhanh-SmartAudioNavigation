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

        // ── PoiPoint ─────────────────────────────────────────────────────────
        builder.Entity<PoiPoint>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.ShortDescription).HasMaxLength(500);
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.MapUrl).HasMaxLength(500);

            // 1 POI có nhiều AudioScript, xoá POI thì xoá script theo
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

            // Mỗi POI chỉ có 1 script cho mỗi ngôn ngữ
            e.HasIndex(a => new { a.PoiPointId, a.LanguageCode }).IsUnique();
        });

        // ── PlaybackLog ───────────────────────────────────────────────────────
        builder.Entity<PlaybackLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.LanguageCode).HasMaxLength(10);
            e.Property(l => l.TriggerType).HasMaxLength(50);
            e.Property(l => l.AnonymousDeviceId).HasMaxLength(100);

            // Index để query analytics nhanh hơn
            e.HasIndex(l => l.PlayedAt);
            e.HasIndex(l => l.PoiPointId);

            // Không cascade — giữ log lại khi xoá POI để không mất analytics
            e.HasOne(l => l.PoiPoint)
             .WithMany()
             .HasForeignKey(l => l.PoiPointId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed data mẫu phố Vĩnh Khánh ─────────────────────────────────────
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.Entity<PoiPoint>().HasData(
            new PoiPoint
            {
                Id = 1, Name = "Bánh mì Cô Ba",
                ShortDescription = "Tiệm bánh mì lâu đời đầu phố Vĩnh Khánh",
                Latitude = 10.7567, Longitude = 106.6997,
                TriggerRadiusMeters = 50, Priority = 5,
                IsActive = true, CreatedAt = now, UpdatedAt = now
            },
            new PoiPoint
            {
                Id = 2, Name = "Hủ tiếu Nam Vang số 1",
                ShortDescription = "Hủ tiếu truyền thống hơn 30 năm",
                Latitude = 10.7570, Longitude = 106.7002,
                TriggerRadiusMeters = 50, Priority = 4,
                IsActive = true, CreatedAt = now, UpdatedAt = now
            },
            new PoiPoint
            {
                Id = 3, Name = "Cà phê vợt Vĩnh Khánh",
                ShortDescription = "Cà phê vợt lâu đời, đặc trưng của phố",
                Latitude = 10.7573, Longitude = 106.7008,
                TriggerRadiusMeters = 40, Priority = 3,
                IsActive = true, CreatedAt = now, UpdatedAt = now
            }
        );

        builder.Entity<AudioScript>().HasData(
            // POI 1 — Bánh mì Cô Ba
            new AudioScript { Id = 1, PoiPointId = 1, LanguageCode = "vi", TtsScript = "Chào mừng bạn đến với tiệm Bánh mì Cô Ba, một trong những tiệm bánh mì lâu đời và nổi tiếng nhất trên phố ẩm thực Vĩnh Khánh.", UpdatedAt = now },
            new AudioScript { Id = 2, PoiPointId = 1, LanguageCode = "en", TtsScript = "Welcome to Co Ba Bakery, one of the oldest and most famous bread shops on Vinh Khanh food street.", UpdatedAt = now },

            // POI 2 — Hủ tiếu
            new AudioScript { Id = 3, PoiPointId = 2, LanguageCode = "vi", TtsScript = "Đây là quán Hủ tiếu Nam Vang số 1, nổi tiếng với nước dùng đậm đà và topping phong phú, đã phục vụ thực khách hơn 30 năm.", UpdatedAt = now },
            new AudioScript { Id = 4, PoiPointId = 2, LanguageCode = "en", TtsScript = "This is Nam Vang Noodle Soup No.1, famous for its rich broth and generous toppings, serving customers for over 30 years.", UpdatedAt = now },

            // POI 3 — Cà phê vợt
            new AudioScript { Id = 5, PoiPointId = 3, LanguageCode = "vi", TtsScript = "Quán cà phê vợt Vĩnh Khánh, nơi lưu giữ hương vị cà phê truyền thống Sài Gòn với cách pha chế thủ công độc đáo.", UpdatedAt = now }
        );
    }
}
