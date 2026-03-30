using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

public class NarrationEngine
{
    private readonly AppDatabase _db;
    private readonly AudioPlayerService _audioPlayer;
    private bool _isPlaying = false;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string CurrentLanguage { get; set; } = "vi";
    public event Action<string>? NarrationStarted;
    public event Action? NarrationFinished;

    public NarrationEngine(AppDatabase db, AudioPlayerService audioPlayer)
    {
        _db = db;
        _audioPlayer = audioPlayer;
    }

    public async Task PlayAsync(LocalPoi poi, double distanceMeters, Location userLocation,
        string triggerType = "geofence_proximity")
    {
        await _lock.WaitAsync();
        try
        {
            if (_isPlaying) return;
            _isPlaying = true;

            var script = await _db.GetAudioScriptAsync(poi.Id, CurrentLanguage)
                      ?? await _db.GetAudioScriptAsync(poi.Id, "vi");

            if (script == null) return;

            NarrationStarted?.Invoke(poi.Name);

            await _db.InsertLogAsync(new LocalPlaybackLog
            {
                PoiPointId = poi.Id,
                LanguageCode = script.LanguageCode,
                PlayedAt = DateTime.UtcNow,
                UserLatitude = userLocation.Latitude,
                UserLongitude = userLocation.Longitude,
                DistanceMeters = distanceMeters,
                TriggerType = triggerType,
                AnonymousDeviceId = GetDeviceId(),
                IsSynced = false
            });

            // Ưu tiên phát audio file đã tải
            if (script.IsAudioDownloaded && File.Exists(script.LocalAudioPath))
            {
                try
                {
                    await _audioPlayer.PlayAudioAsync(script);
                }
                catch
                {
                    // Fallback sang TTS nếu audio lỗi
                    await SpeakWithTtsAsync(script);
                }
            }
            // Chưa tải → thử tải audio rồi phát
            else if (!string.IsNullOrWhiteSpace(script.AudioFileUrl))
            {
                try
                {
                    await _audioPlayer.PlayAudioAsync(script);
                }
                catch
                {
                    await SpeakWithTtsAsync(script);
                }
            }
            // Không có audio → dùng TTS
            else if (!string.IsNullOrWhiteSpace(script.TtsScript))
            {
                await SpeakWithTtsAsync(script);
            }
        }
        finally
        {
            _isPlaying = false;
            _lock.Release();
            NarrationFinished?.Invoke();
        }
    }

    private async Task SpeakWithTtsAsync(LocalAudioScript script)
    {
        var locale = script.LanguageCode switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "zh" => "zh-CN",
            "ko" => "ko-KR",
            "ja" => "ja-JP",
            _ => "vi-VN"
        };

        var mauiLocale = await GetLocaleAsync(locale);
        await TextToSpeech.SpeakAsync(script.TtsScript, new SpeechOptions
        {
            Locale = mauiLocale,
            Volume = 1.0f,
            Pitch = 1.0f
        });
    }

    private static async Task<Locale?> GetLocaleAsync(string localeStr)
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        return locales.FirstOrDefault(l =>
            l.Language.StartsWith(localeStr.Split('-')[0],
            StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            Preferences.Set("device_id", id);
        }
        return id;
    }
}