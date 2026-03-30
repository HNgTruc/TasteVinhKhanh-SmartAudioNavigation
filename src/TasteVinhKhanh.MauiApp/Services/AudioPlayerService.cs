using System.Net.Http;
using Plugin.Maui.Audio;
using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Tải audio file từ server, lưu local, và phát audio.
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
    /// Đảm bảo audio đã được tải về, rồi phát nó.
    /// </summary>
    public async Task PlayAudioAsync(LocalAudioScript script)
    {
        var localPath = script.LocalAudioPath;

        // Chưa tải → thử tải trước
        if (!script.IsAudioDownloaded || string.IsNullOrEmpty(localPath))
        {
            if (!string.IsNullOrWhiteSpace(script.AudioFileUrl))
            {
                localPath = await DownloadAudioAsync(script);
            }
        }

        // Có file local → phát
        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            await PlayLocalFileAsync(localPath);
        }
    }

    /// <summary>
    /// Tải audio từ URL về thư mục app và đánh dấu đã tải.
    /// </summary>
    private async Task<string?> DownloadAudioAsync(LocalAudioScript script)
    {
        if (string.IsNullOrWhiteSpace(script.AudioFileUrl))
            return null;

        try
        {
            var audioDir = Path.Combine(FileSystem.AppDataDirectory, "audio");
            if (!Directory.Exists(audioDir))
                Directory.CreateDirectory(audioDir);

            var fileExt = Path.GetExtension(new Uri(script.AudioFileUrl).LocalPath);
            if (string.IsNullOrEmpty(fileExt)) fileExt = ".mp3";
            var fileName = $"{script.PoiPointId}_{script.LanguageCode}{fileExt}";
            var localPath = Path.Combine(audioDir, fileName);

            // Skip nếu đã tải rồi
            if (File.Exists(localPath))
            {
                await _db.MarkAudioDownloadedAsync(script.Id, localPath);
                return localPath;
            }

            var bytes = await _http.GetByteArrayAsync(script.AudioFileUrl);
            await File.WriteAllBytesAsync(localPath, bytes);
            await _db.MarkAudioDownloadedAsync(script.Id, localPath);

            return localPath;
        }
        catch
        {
            return null;
        }
    }

    private async Task PlayLocalFileAsync(string path)
    {
        try
        {
            _currentPlayer?.Stop();
            _currentPlayer?.Dispose();

            var audioDir = Path.Combine(FileSystem.AppDataDirectory, "audio");
            var fileName = Path.GetFileName(path);
            var fullPath = Path.Combine(audioDir, fileName);

            if (!File.Exists(fullPath))
                return;

            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            _currentPlayer = AudioManager.Current.CreatePlayer(fileStream);
            _currentPlayer.Play();
            // Đợi phát xong
            while (_currentPlayer.IsPlaying)
                await Task.Delay(100);
        }
        catch
        {
            // Fallback: dùng TTS thay thế
            // Đã xử lý ở NarrationEngine
        }
    }
}
