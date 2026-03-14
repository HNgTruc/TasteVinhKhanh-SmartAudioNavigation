using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

public class NarrationEngine
{
    private readonly AppDatabase _db;
    private bool _isPlaying = false;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string CurrentLanguage { get; set; } = "vi";
    public event Action<string>? NarrationStarted;

    public NarrationEngine(AppDatabase db) => _db = db;

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

            if (script.IsAudioDownloaded && File.Exists(script.LocalAudioPath))
            {
                await Task.Delay(3000);
            }
            else if (!string.IsNullOrWhiteSpace(script.TtsScript))
            {
                // Fix: dùng Locale thay vì Language
                var locale = script.LanguageCode switch
                {
                    "vi" => "vi-VN",
                    "en" => "en-US",
                    "zh" => "zh-CN",
                    "ko" => "ko-KR",
                    "ja" => "ja-JP",
                    _ => "vi-VN"
                };

                await TextToSpeech.SpeakAsync(script.TtsScript, new SpeechOptions
                {
                    Locale = await GetLocaleAsync(locale),
                    Volume = 1.0f,
                    Pitch = 1.0f
                });
            }
        }
        finally
        {
            _isPlaying = false;
            _lock.Release();
        }
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