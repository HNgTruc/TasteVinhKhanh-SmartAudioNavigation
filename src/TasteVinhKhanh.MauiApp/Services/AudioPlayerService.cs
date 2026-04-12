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
    private IAudioPlayer? _currentPlayer;
    private TaskCompletionSource<bool>? _currentTcs;

    /// <summary>
    /// Dừng audio đang phát ngay lập tức.
    /// </summary>
    public void Stop()
    {
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

        _currentPlayer = null;
    }

    /// <summary>
    /// Đảm bảo audio đã tải về local, rồi phát.
    /// Priority: local file → download from protected endpoint → TTS fallback.
    /// </summary>
    public async Task PlayAudioAsync(LocalAudioScript script, CancellationToken ct = default)
    {
        var localPath = script.LocalAudioPath;

        // 1. Đã tải rồi → phát trực tiếp từ local
        if (script.IsAudioDownloaded && !string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            try
            {
                await PlayLocalFileAsync(localPath, ct);
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
                    await PlayLocalFileAsync(savedPath, ct);
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

    private async Task PlayLocalFileAsync(string path, CancellationToken ct = default)
    {
        Stop();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Audio file not found: {path}");

        // Đọc file trước (không truyền ct để tránh crash khi Stop() gọi giữa chừng)
        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(path); }
        catch { throw new FileNotFoundException($"Cannot read audio file: {path}"); }

        using var stream = new MemoryStream(bytes);
        _currentPlayer = AudioManager.Current.CreatePlayer(stream);

        _currentTcs = new TaskCompletionSource<bool>();
        _currentPlayer.PlaybackEnded += (s, e) => _currentTcs.TrySetResult(true);

        _currentPlayer.Play();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            await _currentTcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _currentPlayer?.Stop();
            throw;
        }
        finally
        {
            _currentPlayer?.Dispose();
            _currentPlayer = null;
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
