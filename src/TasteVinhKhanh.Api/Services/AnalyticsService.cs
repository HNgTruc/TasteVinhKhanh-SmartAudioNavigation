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
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var today = await _db.PlaybackLogs
            .Where(l => l.PlayedAt >= todayStart && l.PlayedAt < todayEnd)
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
        // GroupBy chỉ chứa cột primitive (PoiPointId) — navigation property không translate được sang SQL
        var raw = await _db.PlaybackLogs
            .GroupBy(l => l.PoiPointId)
            .Select(g => new { PoiPointId = g.Key, Count = g.Count(), MaxPlayedAt = g.Max(l => l.PlayedAt) })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        var poiIds = raw.Select(r => r.PoiPointId).ToList();
        var poiNames = await _db.PoiPoints
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return raw.Select(r => new TopPoiResult(
            r.PoiPointId,
            poiNames.GetValueOrDefault(r.PoiPointId, "?"),
            r.Count,
            r.MaxPlayedAt
        )).ToList();
    }
}
