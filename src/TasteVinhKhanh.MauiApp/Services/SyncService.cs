using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.MauiApp.Services;

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int UpdatedCount { get; set; }
    public bool FromCache { get; set; }
}

public partial class SyncService : ObservableObject
{
    private readonly HttpClient _http;
    private readonly AppDatabase _db;
    private readonly object _syncStateLock = new();
    private DateTime _lastSyncAttemptAtUtc = DateTime.MinValue;
    private SyncResult _lastSyncResult = new() { Success = true, Message = "", UpdatedCount = 0, FromCache = false };

    [ObservableProperty] private string _syncStatus = "Chưa đồng bộ";
    [ObservableProperty] private bool _isSyncing = false;
    [ObservableProperty] private DateTime? _lastSyncAt;
    private static readonly TimeSpan ActiveHeartbeatInterval = TimeSpan.FromMinutes(1);

    public SyncService(HttpClient http, AppDatabase db)
    {
        _http = http;
        _db = db;
    }

    /// <summary>
    /// Gọi GET /api/sync từ server về.
    /// Lưu vào SQLite local để app chạy offline.
    /// Nếu API lỗi → dùng dữ liệu offline có sẵn.
    /// </summary>
    public async Task<SyncResult> SyncPoisAsync(bool force = false)
    {
        lock (_syncStateLock)
        {
            if (IsSyncing)
                return _lastSyncResult;

            var tooSoon = DateTime.UtcNow - _lastSyncAttemptAtUtc < TimeSpan.FromSeconds(45);
            if (!force && tooSoon)
                return _lastSyncResult;

            _lastSyncAttemptAtUtc = DateTime.UtcNow;
        }

        // ── 1. Kiểm tra mạng ──────────────────────────────────────
        var network = Connectivity.NetworkAccess;
        if (network != NetworkAccess.Internet)
        {
            var cached = await _db.GetAllPoisAsync();
            SyncStatus = $"Offline — có {cached.Count} điểm trong bộ nhớ";
            var result = new SyncResult
            {
                Success = true,
                Message = $"Offline — có {cached.Count} điểm trong bộ nhớ",
                UpdatedCount = cached.Count,
                FromCache = true
            };
            _lastSyncResult = result;
            return result;
        }

        IsSyncing = true;
        SyncStatus = "Đang đồng bộ với server...";

        try
        {
            // ── 2. Gọi API ───────────────────────────────────────────
            var lastSync = await _db.GetLastSyncAtAsync();
            var url = lastSync.HasValue
                ? $"api/sync?lastSyncAt={lastSync.Value:O}"
                : "api/sync";

            SyncStatus = $"Đang gọi: {url}";

            // Tăng timeout cho mạng di động/tunnel để giảm false-timeout khi tải POI lần đầu.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _http.GetFromJsonAsync<SyncResponse>(url, cts.Token);

            if (response == null)
                throw new InvalidOperationException("Server trả về dữ liệu rỗng");

            // Chuyển SyncedAt về UTC (server trả UTC, SQLite lưu local → cần UTC để query đúng)
            var utcSyncedAt = response.SyncedAt.Kind == DateTimeKind.Utc
                ? response.SyncedAt
                : response.SyncedAt.ToUniversalTime();

            // Lưu dù có thay đổi hay không (kể cả full sync)
            if (response.HasChanges || response.Pois.Count > 0)
            {
                await _db.UpsertPoisFromServerAsync(response.Pois);
                await _db.SaveLastSyncAtAsync(utcSyncedAt);
                LastSyncAt = utcSyncedAt;
                SyncStatus = $"Đã cập nhật {response.Pois.Count} điểm từ server";

                var result = new SyncResult
                {
                    Success = true,
                    UpdatedCount = response.Pois.Count,
                    Message = $"Đã cập nhật {response.Pois.Count} điểm từ server"
                };
                _lastSyncResult = result;
                return result;
            }

            // Fallback: filter trả 0 → gọi full sync để đảm bảo dữ liệu đầy đủ
            if (lastSync.HasValue)
            {
                SyncStatus = "Full refresh...";
                var full = await _http.GetFromJsonAsync<SyncResponse>("api/sync", cts.Token);
                if (full != null && full.Pois.Count > 0)
                {
                    await _db.UpsertPoisFromServerAsync(full.Pois);
                    var fullUtc = full.SyncedAt.Kind == DateTimeKind.Utc
                        ? full.SyncedAt
                        : full.SyncedAt.ToUniversalTime();
                    await _db.SaveLastSyncAtAsync(fullUtc);
                    LastSyncAt = fullUtc;
                    SyncStatus = $"Full: {full.Pois.Count} điểm";
                    var result = new SyncResult
                    {
                        Success = true,
                        UpdatedCount = full.Pois.Count,
                        Message = $"Full refresh: {full.Pois.Count} điểm"
                    };
                    _lastSyncResult = result;
                    return result;
                }
            }

            await _db.SaveLastSyncAtAsync(utcSyncedAt);
            LastSyncAt = utcSyncedAt;
            SyncStatus = "";

            var finalResult = new SyncResult
            {
                Success = true,
                Message = "",
                UpdatedCount = 0
            };
            _lastSyncResult = finalResult;
            return finalResult;
        }
        catch (OperationCanceledException)
        {
            // Timeout — API không phản hồi
            SyncStatus = "⚠️ Server không phản hồi (timeout)";
            var result = await BuildOfflineResult("Server không phản hồi sau 30 giây");
            _lastSyncResult = result;
            return result;
        }
        catch (HttpRequestException ex)
        {
            // Không kết nối được — có thể API chưa chạy
            SyncStatus = $"⚠️ Không kết nối được server: {ex.Message}";
            var result = await BuildOfflineResult($"Không kết nối server: {ex.Message}");
            _lastSyncResult = result;
            return result;
        }
        catch (Exception ex)
        {
            SyncStatus = $"⚠️ Lỗi sync: {ex.Message}";
            var result = await BuildOfflineResult($"Lỗi: {ex.Message}");
            _lastSyncResult = result;
            return result;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    /// <summary>
    /// Fallback: trả về dữ liệu offline từ SQLite.
    /// </summary>
    private async Task<SyncResult> BuildOfflineResult(string reason)
    {
        var cached = await _db.GetAllPoisAsync();
        if (cached.Count > 0)
        {
            SyncStatus = $"📴 Offline — {cached.Count} điểm (đã lưu trước đó)";
            return new SyncResult
            {
                Success = true,
                Message = $"{reason}. Dùng {cached.Count} điểm offline.",
                UpdatedCount = cached.Count,
                FromCache = true
            };
        }

        SyncStatus = "❌ Không có dữ liệu offline";
        return new SyncResult
        {
            Success = false,
            Message = $"{reason}. Không có dữ liệu offline.",
            UpdatedCount = 0,
            FromCache = true
        };
    }

    /// <summary>
    /// Gửi log chưa đồng bộ lên server khi có mạng.
    /// Chạy nền, không ảnh hưởng UX.
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resp = await _http.PostAsJsonAsync("api/analytics/logs", req, cts.Token);
            if (resp.IsSuccessStatusCode)
                await _db.MarkLogsSyncedAsync(logs.Select(l => l.Id));
        }
        catch
        {
            // Chạy nền — bỏ qua lỗi
        }
    }

    /// <summary>
    /// Gửi heartbeat để đánh dấu thiết bị đang online, không phụ thuộc vào việc phát audio.
    /// Dùng triggerType = app_active để backend tách khỏi analytics nghe audio.
    /// </summary>
    public async Task UploadActiveHeartbeatAsync()
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet) return;

        var nowUtc = DateTime.UtcNow;
        var lastSentTicks = Preferences.Get("active_heartbeat_last_sent_utc_ticks", 0L);
        if (lastSentTicks > 0)
        {
            var lastSentUtc = new DateTime(lastSentTicks, DateTimeKind.Utc);
            if (nowUtc - lastSentUtc < ActiveHeartbeatInterval) return;
        }

        var deviceId = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString();
            Preferences.Set("device_id", deviceId);
        }

        try
        {
            var req = new BatchPlaybackLogRequest
            {
                Logs = new List<PlaybackLogRequest>
                {
                    new()
                    {
                        PoiPointId = 0,
                        LanguageCode = "vi",
                        PlayedAt = nowUtc,
                        UserLatitude = 0,
                        UserLongitude = 0,
                        DistanceMeters = 0,
                        TriggerType = "app_active",
                        AnonymousDeviceId = deviceId
                    }
                }
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var resp = await _http.PostAsJsonAsync("api/analytics/logs", req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                Preferences.Set("active_heartbeat_last_sent_utc_ticks", nowUtc.Ticks);
            }
        }
        catch
        {
            // Chạy nền — bỏ qua lỗi
        }
    }
}
