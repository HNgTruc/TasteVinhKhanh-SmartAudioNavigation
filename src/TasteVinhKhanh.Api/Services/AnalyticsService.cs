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
    Task<HeatmapDataDto> GetHeatmapDataAsync(DateTime? from = null, DateTime? to = null);
    Task<List<HeatmapByHourDto>> GetHeatmapByHourAsync(DateTime? from = null, DateTime? to = null);
    Task<UsageHistoryResponseDto> GetUsageHistoryAsync(UsageHistoryFilterDto filter, int page = 1, int pageSize = 50);
    Task<List<TopDeviceDto>> GetTopDevicesAsync(int top = 20);
    Task<ActiveUsersDto> GetActiveUsersAsync(int windowMinutes = 5);
}

public record AnalyticsSummary(int TotalPlays, int TodayPlays, int UniqueDevices);
public record TopPoiResult(int PoiPointId, string PoiName, int PlayCount, DateTime LastPlayedAt);
public record HeatmapPointLatLng(double Latitude, double Longitude, int Weight);
public record HeatmapByHourResult(int Hour, int Count);
public record TopDeviceResult(string DeviceId, int TotalPlays, int UniquePois, DateTime? FirstPlay, DateTime? LastPlay);

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;
    private const string AppActiveTrigger = "app_active";

    public AnalyticsService(AppDbContext db) => _db = db;

    private IQueryable<PlaybackLog> QueryPlaybackLogs(bool includeAppActive = false)
    {
        var query = _db.PlaybackLogs.AsNoTracking().AsQueryable();
        if (!includeAppActive)
        {
            query = query.Where(l => l.TriggerType != AppActiveTrigger);
        }
        return query;
    }

    /// <summary>Nhận batch log từ MauiApp gửi lên — lưu vào SQL Server</summary>
    public async Task SaveLogsAsync(List<PlaybackLogRequest> logs)
    {
        if (logs.Count == 0) return;

        var fallbackPoiId = await _db.PoiPoints
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (fallbackPoiId <= 0) return;

        var requestedPoiIds = logs
            .Where(l => l.PoiPointId > 0)
            .Select(l => l.PoiPointId)
            .Distinct()
            .ToList();

        var validPoiIds = requestedPoiIds.Count == 0
            ? new HashSet<int>()
            : (await _db.PoiPoints
                .AsNoTracking()
                .Where(p => requestedPoiIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync())
            .ToHashSet();

        var entities = logs
            .Select(l =>
            {
                var resolvedPoiId = l.PoiPointId;
                var isAppActive = string.Equals(l.TriggerType, AppActiveTrigger, StringComparison.OrdinalIgnoreCase);
                if (isAppActive && (resolvedPoiId <= 0 || !validPoiIds.Contains(resolvedPoiId)))
                {
                    resolvedPoiId = fallbackPoiId;
                }

                return new { Log = l, ResolvedPoiId = resolvedPoiId, IsAppActive = isAppActive };
            })
            .Where(x =>
                x.ResolvedPoiId > 0 &&
                (x.IsAppActive || validPoiIds.Contains(x.ResolvedPoiId)))
            .Select(x => new PlaybackLog
        {
            PoiPointId = x.ResolvedPoiId,
            LanguageCode = x.Log.LanguageCode,
            PlayedAt = x.Log.PlayedAt,
            UserLatitude = x.Log.UserLatitude,
            UserLongitude = x.Log.UserLongitude,
            DistanceMeters = x.Log.DistanceMeters,
            TriggerType = x.Log.TriggerType,
            AnonymousDeviceId = x.Log.AnonymousDeviceId,
            IsSynced = true
        })
            .ToList();

        if (entities.Count == 0) return;

        await _db.PlaybackLogs.AddRangeAsync(entities);
        await _db.SaveChangesAsync();
    }

    /// <summary>Tổng quan cho Dashboard của Admin</summary>
    public async Task<AnalyticsSummary> GetSummaryAsync()
    {
        var query = QueryPlaybackLogs();
        var total = await query.CountAsync();
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var today = await query
            .Where(l => l.PlayedAt >= todayStart && l.PlayedAt < todayEnd)
            .CountAsync();
        var uniqueDevices = await QueryPlaybackLogs(includeAppActive: true)
            .Select(l => l.AnonymousDeviceId)
            .Distinct()
            .CountAsync();

        return new AnalyticsSummary(total, today, uniqueDevices);
    }

    /// <summary>Top POI được nghe nhiều nhất</summary>
    public async Task<List<TopPoiResult>> GetTopPoisAsync(int top = 10)
    {
        // GroupBy chỉ chứa cột primitive (PoiPointId) — navigation property không translate được sang SQL
        var raw = await QueryPlaybackLogs()
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
            poiNames.TryGetValue(r.PoiPointId, out var name) ? name : "?",
            r.Count,
            r.MaxPlayedAt
        )).ToList();
    }

    /// <summary>Bản đồ nhiệt — tọa độ + số lượt phát của mỗi điểm</summary>
    public async Task<HeatmapDataDto> GetHeatmapDataAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = QueryPlaybackLogs();

        if (from.HasValue) query = query.Where(l => l.PlayedAt >= from.Value);
        if (to.HasValue)   query = query.Where(l => l.PlayedAt < to.Value.AddDays(1));

        var raw = await query
            .Where(l => l.UserLatitude != 0 && l.UserLongitude != 0)
            .GroupBy(l => new { l.UserLatitude, l.UserLongitude })
            .Select(g => new HeatmapPointLatLng(
                g.Key.UserLatitude,
                g.Key.UserLongitude,
                g.Count()))
            .ToListAsync();

        return new HeatmapDataDto
        {
            Points = raw.Select(r => new HeatmapPointDto
            {
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Weight = r.Weight
            }).ToList(),
            TotalCount = raw.Sum(r => r.Weight)
        };
    }

    /// <summary>Heatmap theo giờ trong ngày (0–23)</summary>
    public async Task<List<HeatmapByHourDto>> GetHeatmapByHourAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = QueryPlaybackLogs();

        if (from.HasValue) query = query.Where(l => l.PlayedAt >= from.Value);
        if (to.HasValue)   query = query.Where(l => l.PlayedAt < to.Value.AddDays(1));

        // Tính theo giờ ở memory để tránh lỗi translate DateTime.Hour của provider SQL.
        var playedAts = await query
            .Select(l => l.PlayedAt)
            .ToListAsync();

        var raw = playedAts
            .GroupBy(t => t.Hour)
            .Select(g => new HeatmapByHourResult(g.Key, g.Count()))
            .OrderBy(r => r.Hour)
            .ToList();

        // Điền đầy đủ 24 giờ (giờ không có dữ liệu → 0)
        return Enumerable.Range(0, 24)
            .Select(h => raw.FirstOrDefault(r => r.Hour == h) ?? new HeatmapByHourResult(h, 0))
            .Select(r => new HeatmapByHourDto { Hour = r.Hour, Count = r.Count })
            .ToList();
    }

    /// <summary>Lịch sử nghe chi tiết — có phân trang và filter</summary>
    public async Task<UsageHistoryResponseDto> GetUsageHistoryAsync(
        UsageHistoryFilterDto filter, int page = 1, int pageSize = 50)
    {
        var query = QueryPlaybackLogs();

        if (filter.PoiPointId.HasValue)
            query = query.Where(l => l.PoiPointId == filter.PoiPointId.Value);

        if (!string.IsNullOrWhiteSpace(filter.DeviceId))
            query = query.Where(l => l.AnonymousDeviceId == filter.DeviceId);

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.PlayedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(l => l.PlayedAt < filter.ToDate.Value.AddDays(1));

        var totalCount = await query.CountAsync();

        var poiIds = await query
            .Select(l => l.PoiPointId)
            .Distinct()
            .ToListAsync();

        var poiNames = await _db.PoiPoints
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var items = await query
            .OrderByDescending(l => l.PlayedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new UsageHistoryItemDto
            {
                Id = l.Id,
                PoiPointId = l.PoiPointId,
                PoiName = string.Empty,
                LanguageCode = l.LanguageCode,
                TriggerType = l.TriggerType,
                DistanceMeters = l.DistanceMeters,
                PlayedAt = l.PlayedAt,
                DeviceId = l.AnonymousDeviceId
            })
            .ToListAsync();

        foreach (var item in items)
            item.PoiName = poiNames.TryGetValue(item.PoiPointId, out var name) ? name : "?";

        return new UsageHistoryResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>Top thiết bị hoạt động nhiều nhất</summary>
    public async Task<List<TopDeviceDto>> GetTopDevicesAsync(int top = 20)
    {
        var logs = await QueryPlaybackLogs()
            .Select(l => new { l.AnonymousDeviceId, l.PoiPointId, l.PlayedAt })
            .ToListAsync();

        var raw = logs
            .Where(l => !string.IsNullOrWhiteSpace(l.AnonymousDeviceId))
            .GroupBy(l => l.AnonymousDeviceId)
            .Select(g => new TopDeviceResult(
                g.Key,
                g.Count(),
                g.Select(x => x.PoiPointId).Distinct().Count(),
                g.Min(x => x.PlayedAt),
                g.Max(x => x.PlayedAt)
            ))
            .OrderByDescending(r => r.TotalPlays)
            .Take(top)
            .ToList();

        return raw.Select(r => new TopDeviceDto
        {
            DeviceId = r.DeviceId,
            TotalPlays = r.TotalPlays,
            UniquePois = r.UniquePois,
            FirstPlay = r.FirstPlay,
            LastPlay = r.LastPlay ?? DateTime.UtcNow
        }).ToList();
    }

    /// <summary>Số thiết bị đang hoạt động trong N phút gần nhất.</summary>
    public async Task<ActiveUsersDto> GetActiveUsersAsync(int windowMinutes = 1)
    {
        if (windowMinutes <= 0) windowMinutes = 1;
        if (windowMinutes > 120) windowMinutes = 120;

        var fromUtc = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var activeUsers = await QueryPlaybackLogs(includeAppActive: true)
            .Where(l => l.PlayedAt >= fromUtc && !string.IsNullOrWhiteSpace(l.AnonymousDeviceId))
            .Select(l => l.AnonymousDeviceId)
            .Distinct()
            .CountAsync();

        return new ActiveUsersDto
        {
            ActiveUsers = activeUsers,
            WindowMinutes = windowMinutes,
            CalculatedAtUtc = DateTime.UtcNow
        };
    }
}
