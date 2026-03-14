using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Services;

public interface IAnalyticsService
{
    Task SaveLogsAsync(List<PlaybackLogRequest> logs);
    Task<AnalyticsSummary> GetSummaryAsync();
    Task<List<TopPoiResult>> GetTopPoisAsync(int top = 10);
}

public record AnalyticsSummary(int TotalPlays, int TodayPlays, int UniqueDevices);
public record TopPoiResult(int PoiPointId, string PoiName, int PlayCount, DateTime LastPlayedAt);

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    /// <summary>Nhận batch log từ MauiApp gửi lên — lưu vào SQL Server</summary>
    public async Task SaveLogsAsync(List<PlaybackLogRequest> logs)
    {
        var entities = logs.Select(l => new PlaybackLog
        {
            PoiPointId = l.PoiPointId,
            LanguageCode = l.LanguageCode,
            PlayedAt = l.PlayedAt,
            UserLatitude = l.UserLatitude,
            UserLongitude = l.UserLongitude,
            DistanceMeters = l.DistanceMeters,
            TriggerType = l.TriggerType,
            AnonymousDeviceId = l.AnonymousDeviceId,
            IsSynced = true
        });

        await _db.PlaybackLogs.AddRangeAsync(entities);
        await _db.SaveChangesAsync();
    }

    /// <summary>Tổng quan cho Dashboard của Admin</summary>
    public async Task<AnalyticsSummary> GetSummaryAsync()
    {
        var total = await _db.PlaybackLogs.CountAsync();
        var today = await _db.PlaybackLogs
            .Where(l => l.PlayedAt.Date == DateTime.UtcNow.Date)
            .CountAsync();
        var uniqueDevices = await _db.PlaybackLogs
            .Select(l => l.AnonymousDeviceId)
            .Distinct()
            .CountAsync();

        return new AnalyticsSummary(total, today, uniqueDevices);
    }

    /// <summary>Top POI được nghe nhiều nhất</summary>
    public async Task<List<TopPoiResult>> GetTopPoisAsync(int top = 10)
    {
        return await _db.PlaybackLogs
            .GroupBy(l => new { l.PoiPointId, l.PoiPoint!.Name })
            .Select(g => new TopPoiResult(
                g.Key.PoiPointId,
                g.Key.Name,
                g.Count(),
                g.Max(l => l.PlayedAt)
            ))
            .OrderByDescending(x => x.PlayCount)
            .Take(top)
            .ToListAsync();
    }
}
