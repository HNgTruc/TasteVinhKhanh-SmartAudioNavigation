using System.Net.Http.Headers;
using System.Net.Http.Json;
using Plugin.Maui.Audio;
using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Tải audio file từ protected endpoint /api/audio/{scriptId},
/// lưu local trong FileSystem.AppDataDirectory/audio/,
/// và phát audio. Hỗ trợ offline từ cache local.
/// </summary>
public class AudioPlayerService
{
    private readonly HttpClient _http;
    private readonly AppDatabase _db;
    private IAudioPlayer? _currentPlayer;

    public AudioPlayerService(HttpClient http, AppDatabase db)
    {
        _http = http;
        _db = db;
    }

    /// <summary>
    /// Đảm bảo audio đã tải về local, rồi phát.
    /// Priority: local file → download from protected endpoint → TTS fallback.
    /// </summary>
    public async Task PlayAudioAsync(LocalAudioScript script)
    {
        var localPath = script.LocalAudioPath;

        // 1. Đã tải rồi → phát trực tiếp từ local
        if (script.IsAudioDownloaded && !string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            try
            {
                await PlayLocalFileAsync(localPath);
                return;
            }
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
                    await PlayLocalFileAsync(savedPath);
                return;
            }
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
            await _db.UpdateAudioDownloadedAsync(script.Id, localPath);
            return true;
        }

        try
        {
            // Dùng _http.GetAsync với BaseAddress đã set (từ MauiProgram.cs DI)
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/audio/{script.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes == null || bytes.Length == 0) return false;

            await File.WriteAllBytesAsync(localPath, bytes);
            script.LocalAudioPath = localPath;
            script.IsAudioDownloaded = true;
            await _db.UpdateAudioDownloadedAsync(script.Id, localPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PlayLocalFileAsync(string path)
    {
        _currentPlayer?.Stop();
        _currentPlayer?.Dispose();
        _currentPlayer = null;

        if (!File.Exists(path))
            throw new FileNotFoundException($"Audio file not found: {path}");

        // Đọc bytes rồi tạo stream — tránh FileStream bị lock
        var bytes = await File.ReadAllBytesAsync(path);
        using var stream = new MemoryStream(bytes);
        _currentPlayer = AudioManager.Current.CreatePlayer(stream);

        // Lắng nghe sự kiện kết thúc
        var tcs = new TaskCompletionSource<bool>();
        _currentPlayer.PlaybackEnded += () => tcs.TrySetResult(true);

        _currentPlayer.Play();

        // Chờ phát xong hoặc timeout 60s
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await tcs.Task.WaitAsync(cts.Token);
        }
        catch (TimeoutException)
        {
            _currentPlayer?.Stop();
        }
        finally
        {
            _currentPlayer?.Dispose();
            _currentPlayer = null;
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
            var res = await _http.PostAsJsonAsync(
                "api/auth/device-register",
                new { deviceId });

            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<DeviceTokenResult>();
                if (result != null && !string.IsNullOrEmpty(result.accessToken))
                {
                    Preferences.Set("device_token", result.accessToken);
                    return result.accessToken;
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
        public string accessToken { get; set; } = string.Empty;
        public DateTime expiresAt { get; set; }
    }
}
