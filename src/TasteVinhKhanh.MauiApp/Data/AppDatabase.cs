using SQLite;
using System.ComponentModel.DataAnnotations.Schema;
using TasteVinhKhanh.Shared.DTOs;

using TableAttribute = SQLite.TableAttribute;

namespace TasteVinhKhanh.MauiApp.Data;

[Table("PoiPoints")]
public class LocalPoi
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadiusMeters { get; set; } = 50;
    public int Priority { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? MapUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("AudioScripts")]
public class LocalAudioScript
{
    [PrimaryKey]
    public int Id { get; set; }
    [Indexed]
    public int PoiPointId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string TtsScript { get; set; } = string.Empty;
    public string? AudioFileUrl { get; set; }
    public string? LocalAudioPath { get; set; }
    public bool IsAudioDownloaded { get; set; } = false;
    public DateTime UpdatedAt { get; set; }
}

[Table("PlaybackLogs")]
public class LocalPlaybackLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int PoiPointId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
    public double UserLatitude { get; set; }
    public double UserLongitude { get; set; }
    public double DistanceMeters { get; set; }
    public string TriggerType { get; set; } = "geofence_proximity";
    public string AnonymousDeviceId { get; set; } = string.Empty;
    public bool IsSynced { get; set; } = false;
}

[Table("SyncMeta")]
public class SyncMeta
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public DateTime LastSyncAt { get; set; }
}

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AppDatabase()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "vinhkhanh.db3");
        _db = new SQLiteAsyncConnection(path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _db.CreateTableAsync<LocalPoi>();
            await _db.CreateTableAsync<LocalAudioScript>();
            await _db.CreateTableAsync<LocalPlaybackLog>();
            await _db.CreateTableAsync<SyncMeta>();
            _initialized = true;
        }
        finally { _lock.Release(); }
    }

    // ── POI ───────────────────────────────────────────────────

    public Task<List<LocalPoi>> GetAllPoisAsync()
        => _db.Table<LocalPoi>().Where(p => p.IsActive).ToListAsync();

    public Task<LocalPoi?> GetPoiByIdAsync(int id)
        => _db.Table<LocalPoi>().Where(p => p.Id == id).FirstOrDefaultAsync();

    public async Task UpsertPoisFromServerAsync(IEnumerable<PoiDto> dtos)
    {
        await _db.RunInTransactionAsync(db =>
        {
            foreach (var dto in dtos)
            {
                var local = new LocalPoi
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    ShortDescription = dto.ShortDescription,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    TriggerRadiusMeters = dto.TriggerRadiusMeters,
                    Priority = dto.Priority,
                    IsActive = dto.IsActive,
                    ImageUrl = dto.ImageUrl,
                    MapUrl = dto.MapUrl,
                    UpdatedAt = dto.UpdatedAt
                };

                var exists = db.Table<LocalPoi>().Where(p => p.Id == dto.Id).FirstOrDefault();
                if (exists == null) db.Insert(local); else db.Update(local);

                foreach (var s in dto.AudioScripts)
                {
                    var existScript = db.Table<LocalAudioScript>()
                        .Where(x => x.PoiPointId == dto.Id && x.LanguageCode == s.LanguageCode)
                        .FirstOrDefault();

                    if (existScript == null)
                    {
                        db.Insert(new LocalAudioScript
                        {
                            Id = s.Id,
                            PoiPointId = s.PoiPointId,
                            LanguageCode = s.LanguageCode,
                            TtsScript = s.TtsScript,
                            AudioFileUrl = s.AudioFileUrl,
                            UpdatedAt = s.UpdatedAt
                        });
                    }
                    else
                    {
                        // Giữ nguyên LocalAudioPath nếu đã tải về
                        existScript.TtsScript = s.TtsScript;
                        existScript.AudioFileUrl = s.AudioFileUrl;
                        existScript.UpdatedAt = s.UpdatedAt;
                        db.Update(existScript);
                    }
                }
            }
        });
    }

    // ── AUDIO ─────────────────────────────────────────────────

    public Task<LocalAudioScript?> GetAudioScriptAsync(int poiId, string lang)
        => _db.Table<LocalAudioScript>()
              .Where(s => s.PoiPointId == poiId && s.LanguageCode == lang)
              .FirstOrDefaultAsync();

    public async Task MarkAudioDownloadedAsync(int scriptId, string localPath)
    {
        var s = await _db.Table<LocalAudioScript>()
                         .Where(x => x.Id == scriptId).FirstOrDefaultAsync();
        if (s == null) return;
        s.LocalAudioPath = localPath;
        s.IsAudioDownloaded = true;
        await _db.UpdateAsync(s);
    }

    // ── PLAYBACK LOG ──────────────────────────────────────────

    public Task InsertLogAsync(LocalPlaybackLog log) => _db.InsertAsync(log);

    public Task<List<LocalPlaybackLog>> GetUnsyncedLogsAsync()
        => _db.Table<LocalPlaybackLog>().Where(l => !l.IsSynced).ToListAsync();

    public async Task MarkLogsSyncedAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            var l = await _db.Table<LocalPlaybackLog>()
                             .Where(x => x.Id == id).FirstOrDefaultAsync();
            if (l == null) continue;
            l.IsSynced = true;
            await _db.UpdateAsync(l);
        }
    }

    /// <summary>Kiểm tra cooldown — tránh phát lại cùng POI quá nhanh</summary>
    public async Task<bool> WasRecentlyPlayedAsync(int poiId, TimeSpan cooldown)
    {
        var cutoff = DateTime.UtcNow - cooldown;
        return await _db.Table<LocalPlaybackLog>()
            .Where(l => l.PoiPointId == poiId && l.PlayedAt >= cutoff)
            .CountAsync() > 0;
    }

    // ── SYNC META ─────────────────────────────────────────────

    public async Task<DateTime?> GetLastSyncAtAsync()
    {
        var m = await _db.Table<SyncMeta>().FirstOrDefaultAsync();
        return m?.LastSyncAt;
    }

    public async Task SaveLastSyncAtAsync(DateTime at)
    {
        var m = await _db.Table<SyncMeta>().FirstOrDefaultAsync();
        if (m == null) await _db.InsertAsync(new SyncMeta { LastSyncAt = at });
        else { m.LastSyncAt = at; await _db.UpdateAsync(m); }
    }
}