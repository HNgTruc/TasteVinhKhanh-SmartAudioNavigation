using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Services;

public interface IPoiService
{
    Task<List<PoiDto>> GetAllAsync(bool includeInactive = false);
    Task<PoiDto?> GetByIdAsync(int id);
    Task<PoiDto> CreateAsync(CreatePoiRequest request);
    Task<PoiDto?> UpdateAsync(int id, UpdatePoiRequest request);
    Task<bool> DeleteAsync(int id);
    Task<AudioScriptDto> UpsertScriptAsync(int poiId, UpsertAudioScriptRequest request);
    Task<bool> DeleteScriptAsync(int poiId, string languageCode);
}

public class PoiService : IPoiService
{
    private readonly AppDbContext _db;

    public PoiService(AppDbContext db) => _db = db;

    public async Task<List<PoiDto>> GetAllAsync(bool includeInactive = false)
    {
        var query = _db.PoiPoints.Include(p => p.AudioScripts).AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var list = await query.OrderByDescending(p => p.Priority).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PoiDto?> GetByIdAsync(int id)
    {
        var poi = await _db.PoiPoints
            .Include(p => p.AudioScripts)
            .FirstOrDefaultAsync(p => p.Id == id);

        return poi == null ? null : ToDto(poi);
    }

    public async Task<PoiDto> CreateAsync(CreatePoiRequest r)
    {
        var poi = new PoiPoint
        {
            Name = r.Name,
            ShortDescription = r.ShortDescription,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            TriggerRadiusMeters = r.TriggerRadiusMeters,
            Priority = r.Priority,
            ImageUrl = r.ImageUrl,
            MapUrl = r.MapUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PoiPoints.Add(poi);
        await _db.SaveChangesAsync();
        return ToDto(poi);
    }

    public async Task<PoiDto?> UpdateAsync(int id, UpdatePoiRequest r)
    {
        var poi = await _db.PoiPoints.FindAsync(id);
        if (poi == null) return null;

        poi.Name = r.Name;
        poi.ShortDescription = r.ShortDescription;
        poi.Latitude = r.Latitude;
        poi.Longitude = r.Longitude;
        poi.TriggerRadiusMeters = r.TriggerRadiusMeters;
        poi.Priority = r.Priority;
        poi.ImageUrl = r.ImageUrl;
        poi.MapUrl = r.MapUrl;
        poi.IsActive = r.IsActive;
        poi.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(poi);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var poi = await _db.PoiPoints.FindAsync(id);
        if (poi == null) return false;

        // Soft delete — ẩn đi thay vì xoá thật, giữ lại analytics
        poi.IsActive = false;
        poi.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AudioScriptDto> UpsertScriptAsync(int poiId, UpsertAudioScriptRequest r)
    {
        var script = await _db.AudioScripts
            .FirstOrDefaultAsync(s => s.PoiPointId == poiId && s.LanguageCode == r.LanguageCode);

        if (script == null)
        {
            script = new AudioScript { PoiPointId = poiId, LanguageCode = r.LanguageCode };
            _db.AudioScripts.Add(script);
        }

        script.TtsScript = r.TtsScript;
        script.AudioFileUrl = r.AudioFileUrl;
        script.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToScriptDto(script);
    }

    public async Task<bool> DeleteScriptAsync(int poiId, string languageCode)
    {
        var script = await _db.AudioScripts
            .FirstOrDefaultAsync(s => s.PoiPointId == poiId && s.LanguageCode == languageCode);
        if (script == null) return false;

        _db.AudioScripts.Remove(script);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static PoiDto ToDto(PoiPoint p) => new()
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
        AudioScripts = p.AudioScripts.Select(ToScriptDto).ToList()
    };

    private static AudioScriptDto ToScriptDto(AudioScript s) => new()
    {
        Id = s.Id,
        PoiPointId = s.PoiPointId,
        LanguageCode = s.LanguageCode,
        TtsScript = s.TtsScript,
        AudioFileUrl = s.AudioFileUrl,
        UpdatedAt = s.UpdatedAt
    };
}
