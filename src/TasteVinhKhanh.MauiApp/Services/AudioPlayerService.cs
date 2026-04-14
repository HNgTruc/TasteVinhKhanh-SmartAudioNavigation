using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Plugin.Maui.Audio;
using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Tải audio file từ protected endpoint /api/audio/{scriptId},
/// lưu local trong FileSystem.AppDataDirectory/audio/,
/// và phát audio. Hỗ trợ offline từ cache local.
/// </summary>
public class AudioPlayerService(HttpClient http, AppDatabase db)
{
    private IAudioPlayer? _currentPlayer; // dùng cho Pause/Play/Seek/Duration
    private AsyncAudioPlayer? _asyncPlayer; // dùng cho PlayAsync (await)
    private TaskCompletionSource<bool>? _currentTcs;
    private CancellationTokenSource? _positionTimerCts;

    /// <summary>
    /// Fire định kỳ khi audio đang phát: (currentPosition seconds, totalDuration seconds).
    /// </summary>
    public event Action<double, double>? PlaybackPositionChanged;

    /// <summary>
    /// Dừng timer, audio, và dispose player.
    /// Trả về vị trí đã phát được (tính bằng giây) để có thể resume.
    /// </summary>
    public TimeSpan Stop()
    {
        StopPositionTimer();

        double lastPosSec = 0;
        try
        {
            if (_currentPlayer != null)
                lastPosSec = _currentPlayer.CurrentPosition;
        }
        catch { /* ignore */ }

        try
        {
            _currentTcs?.TrySetCanceled();
        }
        catch { /* ignore if already disposed */ }
        _currentTcs = null;

        try
        {
            _currentPlayer?.Stop();
        }
        catch { /* ignore */ }

        try
        {
            _currentPlayer?.Dispose();
        }
        catch { /* ignore */ }

        try
        {
            _asyncPlayer?.Dispose();
        }
        catch { /* ignore */ }

        _currentPlayer = null;
        _asyncPlayer = null;
        return TimeSpan.FromSeconds(lastPosSec);
    }

    /// <summary>
    /// Đảm bảo audio đã tải về local, rồi phát.
    /// Priority: local file → download from protected endpoint → TTS fallback.
    /// </summary>
    public async Task PlayAudioAsync(LocalAudioScript script,
        CancellationToken ct = default, TimeSpan? startPosition = null)
    {
        var localPath = script.LocalAudioPath;

        // 1. Đã tải rồi → phát trực tiếp từ local
        if (script.IsAudioDownloaded && !string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            try
            {
                await PlayLocalFileAsync(localPath, ct, startPosition);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // File lỗi → xóa cache, thử tải lại
                script.IsAudioDownloaded = false;
                script.LocalAudioPath = null;
            }
        }

        // 2. Chưa tải → tải từ protected endpoint
        if (await DownloadAudioAsync(script))
        {
            try
            {
                var savedPath = Path.Combine(
                    FileSystem.AppDataDirectory, "audio",
                    $"{script.PoiPointId}_{script.LanguageCode}.mp3");
                if (File.Exists(savedPath))
                    await PlayLocalFileAsync(savedPath, ct, startPosition);
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch { /* Fallback TTS */ }
        }

        // 3. Không tải được → TTS fallback (NarrationEngine xử lý)
        throw new InvalidOperationException("Audio download failed");
    }

    /// <summary>
    /// Tải audio từ protected endpoint về thư mục app.
    /// Dùng device token để xác thực.
    /// BaseAddress đã được set trong MauiProgram.cs qua DI.
    /// </summary>
    private async Task<bool> DownloadAudioAsync(LocalAudioScript script)
    {
        // Lấy device token từ Preferences, đăng ký nếu chưa có
        var deviceToken = GetDeviceToken();
        if (string.IsNullOrEmpty(deviceToken))
        {
            deviceToken = await RegisterDeviceAsync();
            if (string.IsNullOrEmpty(deviceToken)) return false;
        }

        var audioDir = Path.Combine(FileSystem.AppDataDirectory, "audio");
        if (!Directory.Exists(audioDir))
            Directory.CreateDirectory(audioDir);

        var localPath = Path.Combine(audioDir, $"{script.PoiPointId}_{script.LanguageCode}.mp3");

        // Skip nếu đã tải rồi
        if (File.Exists(localPath))
        {
            script.LocalAudioPath = localPath;
            script.IsAudioDownloaded = true;
            await db.UpdateAudioDownloadedAsync(script.Id, localPath);
            return true;
        }

        try
        {
            // Dùng _http.GetAsync với BaseAddress đã set (từ MauiProgram.cs DI)
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/audio/{script.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes == null || bytes.Length == 0) return false;

            await File.WriteAllBytesAsync(localPath, bytes);
            script.LocalAudioPath = localPath;
            script.IsAudioDownloaded = true;
            await db.UpdateAudioDownloadedAsync(script.Id, localPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StopPositionTimer()
    {
        try { _positionTimerCts?.Cancel(); _positionTimerCts?.Dispose(); }
        catch { /* ignore */ }
        _positionTimerCts = null;
    }

    /// <summary>
    /// Tạm dừng audio nhưng GIỮ NGUYÊN player để có thể resume đúng vị trí.
    /// </summary>
    public void Pause()
    {
        StopPositionTimer();
        try { _currentPlayer?.Pause(); } catch { /* ignore */ }
    }

    /// <summary>
    /// Trả về vị trí hiện tại của audio đang phát.
    /// </summary>
    public TimeSpan GetCurrentPosition()
    {
        try { return TimeSpan.FromSeconds(_currentPlayer?.CurrentPosition ?? 0); }
        catch { return TimeSpan.Zero; }
    }

    /// <summary>
    /// Tiếp tục phát từ vị trí đã tạm dừng.
    /// </summary>
    public void Resume()
    {
        try { _currentPlayer?.Play(); } catch { /* ignore */ }
        StartPositionTimerLoop(); // timer tự đọc cả position + duration
    }

    private void StartPositionTimerLoop()
    {
        StopPositionTimer();
        _positionTimerCts = new CancellationTokenSource();
        _ = PositionTimerLoop(_positionTimerCts.Token);
    }

    private async Task PositionTimerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _currentPlayer != null)
        {
            try
            {
                var posSec = _currentPlayer.CurrentPosition;
                var durSec = _currentPlayer.Duration;
                if (durSec > 0)
                    PlaybackPositionChanged?.Invoke(posSec, durSec);
            }
            catch { /* ignore */ }
            try { await Task.Delay(250, ct); }
            catch { break; }
        }
    }

    private void StartPositionTimer(double totalDurationSec)
    {
        StopPositionTimer();
        _positionTimerCts = new CancellationTokenSource();
        _ = PositionTimerWithDuration(totalDurationSec, _positionTimerCts.Token);
    }

    private async Task PositionTimerWithDuration(double totalDurationSec, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _currentPlayer != null)
        {
            try
            {
                var posSec = _currentPlayer.CurrentPosition;
                if (totalDurationSec > 0)
                    PlaybackPositionChanged?.Invoke(posSec, totalDurationSec);
            }
            catch { /* ignore */ }
            try { await Task.Delay(250, ct); }
            catch { break; }
        }
    }

    private async Task PlayLocalFileAsync(string path,
        CancellationToken ct = default, TimeSpan? startPosition = null)
    {
        Stop(); // dọn player cũ

        if (!File.Exists(path))
            throw new FileNotFoundException($"Audio file not found: {path}");

        // Đọc file thành stream để tạo player
        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(path); }
        catch { throw new FileNotFoundException($"Cannot read audio file: {path}"); }

        using var stream = new MemoryStream(bytes);
        _currentPlayer = AudioManager.Current.CreatePlayer(stream);

        // Seek đến vị trí resume (nếu có)
        if (startPosition.HasValue && startPosition.Value.TotalSeconds > 0)
            _currentPlayer.Seek(startPosition.Value.TotalSeconds);

        _currentTcs = new TaskCompletionSource<bool>();
        _currentPlayer.PlaybackEnded += (s, e) =>
        {
            StopPositionTimer();
            _currentTcs.TrySetResult(true);
        };

        // Lấy Duration ngay khi player được tạo (từ metadata file audio)
        double totalDurationSec;
        try { totalDurationSec = _currentPlayer.Duration; }
        catch { totalDurationSec = 0; }

        StartPositionTimer(totalDurationSec);
        _currentPlayer.Play();

        // Chờ cho đến khi phát xong hoặc bị cancel
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            await _currentTcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            StopPositionTimer();
            try { _currentPlayer?.Stop(); } catch { /* ignore */ }
            throw;
        }
        finally
        {
            StopPositionTimer();
            try { _currentPlayer?.Dispose(); } catch { /* ignore */ }
            _currentPlayer = null;
            _asyncPlayer = null;
            _currentTcs = null;
        }
    }

    private static string GetDeviceToken()
        => Preferences.Get("device_token", string.Empty);

    /// <summary>
    /// Đăng ký device với server → lấy JWT token.
    /// Gọi 1 lần khi app khởi động.
    /// BaseAddress đã được set từ DI.
    /// </summary>
    public async Task<string> RegisterDeviceAsync()
    {
        var existingToken = GetDeviceToken();
        if (!string.IsNullOrEmpty(existingToken))
            return existingToken;

        var deviceId = GetOrCreateDeviceId();

        try
        {
            // BaseAddress đã set từ MauiProgram.cs DI — chỉ cần endpoint path
            var res = await http.PostAsJsonAsync(
                "api/auth/device-register",
                new { deviceId });

            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<DeviceTokenResult>();
                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    Preferences.Set("device_token", result.AccessToken);
                    return result.AccessToken;
                }
            }
        }
        catch { /* offline — không có token */ }

        return string.Empty;
    }

    private static string GetOrCreateDeviceId()
    {
        var id = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            Preferences.Set("device_id", id);
        }
        return id;
    }

    private class DeviceTokenResult
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }
}
