using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Services;

public interface ISyncService
{
    Task<SyncResponse> GetChangesAsync(DateTime? lastSyncAt);
}

public class SyncService : ISyncService
{
    private readonly AppDbContext _db;

    public SyncService(AppDbContext db) => _db = db;

    /// <summary>
    /// MauiApp gọi endpoint này khi có mạng.
    /// Nếu truyền lastSyncAt thì chỉ trả về POI thay đổi sau thời điểm đó,
    /// giúp tiết kiệm băng thông — không cần tải lại toàn bộ mỗi lần.
    /// </summary>
    public async Task<SyncResponse> GetChangesAsync(DateTime? lastSyncAt)
    {
        var query = _db.PoiPoints.Include(p => p.AudioScripts).AsQueryable();

        if (lastSyncAt.HasValue)
            query = query.Where(p => p.UpdatedAt > lastSyncAt.Value);

        var pois = await query.OrderByDescending(p => p.Priority).ToListAsync();

        return new SyncResponse
        {
            HasChanges = pois.Any(),
            SyncedAt = DateTime.UtcNow,
            Pois = pois.Select(p => new PoiDto
            {
                Id = p.Id,
                Name = p.Name,
                ShortDescription = p.ShortDescription,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                TriggerRadiusMeters = p.TriggerRadiusMeters,
                Priority = p.Priority,
                IsActive = p.IsActive,
                ImageUrl = p.ImageUrl,
                MapUrl = p.MapUrl,
                UpdatedAt = p.UpdatedAt,
                AudioScripts = p.AudioScripts.Select(s => new AudioScriptDto
                {
                    Id = s.Id,
                    PoiPointId = s.PoiPointId,
                    LanguageCode = s.LanguageCode,
                    TtsScript = s.TtsScript,
                    AudioFileUrl = s.AudioFileUrl,
                    UpdatedAt = s.UpdatedAt
                }).ToList()
            }).ToList()
        };
    }
}
