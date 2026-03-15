using System.Net.Http.Json;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.MauiApp.Services;

public class SyncService
{
    private readonly HttpClient _http;
    private readonly AppDatabase _db;

    public SyncService(HttpClient http, AppDatabase db)
    {
        _http = http;
        _db = db;
    }

    /// <summary>
    /// Gọi GET /api/sync từ SQL Server về
    /// Lưu vào SQLite local để app chạy offline
    /// </summary>
    public async Task<SyncResult> SyncPoisAsync()
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            return new SyncResult { Success = false, Message = "Không có mạng, dùng dữ liệu offline" };

        try
        {
            var lastSync = await _db.GetLastSyncAtAsync();
            var url = lastSync.HasValue
                ? $"api/sync?lastSyncAt={lastSync.Value:O}"
                : "api/sync";

            var response = await _http.GetFromJsonAsync<SyncResponse>(url);
            if (response == null)
                return new SyncResult { Success = false, Message = "Server không phản hồi" };

            if (response.HasChanges)
            {
                await _db.UpsertPoisFromServerAsync(response.Pois);
                await _db.SaveLastSyncAtAsync(response.SyncedAt);
                return new SyncResult
                {
                    Success = true,
                    UpdatedCount = response.Pois.Count,
                    Message = $"Đã cập nhật {response.Pois.Count} điểm thuyết minh"
                };
            }

            await _db.SaveLastSyncAtAsync(response.SyncedAt);
            return new SyncResult { Success = true, Message = "Dữ liệu đã là mới nhất" };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Message = $"Lỗi sync: {ex.Message}" };
        }
    }

    /// <summary>
    /// Gửi log chưa đồng bộ lên server khi có mạng
    /// </summary>
    public async Task UploadPendingLogsAsync()
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet) return;
        try
        {
            var logs = await _db.GetUnsyncedLogsAsync();
            if (!logs.Any()) return;

            var req = new BatchPlaybackLogRequest
            {
                Logs = logs.Select(l => new PlaybackLogRequest
                {
                    PoiPointId = l.PoiPointId,
                    LanguageCode = l.LanguageCode,
                    PlayedAt = l.PlayedAt,
                    UserLatitude = l.UserLatitude,
                    UserLongitude = l.UserLongitude,
                    DistanceMeters = l.DistanceMeters,
                    TriggerType = l.TriggerType,
                    AnonymousDeviceId = l.AnonymousDeviceId
                }).ToList()
            };

            var resp = await _http.PostAsJsonAsync("api/analytics/logs", req);
            if (resp.IsSuccessStatusCode)
                await _db.MarkLogsSyncedAsync(logs.Select(l => l.Id));
        }
        catch { }
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int UpdatedCount { get; set; }
}