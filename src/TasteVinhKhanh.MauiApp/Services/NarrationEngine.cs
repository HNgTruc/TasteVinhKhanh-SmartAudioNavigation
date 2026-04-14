using System.Threading;
using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

public class NarrationEngine
{
    private readonly AppDatabase _db;
    private readonly AudioPlayerService _audioPlayer;
    private bool _isPlaying = false;
    private bool _isPaused = false;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _ttsCts;
    private CancellationTokenSource? _playbackCts;

    public string CurrentLanguage { get; set; } = "vi";
    public event Action<string>? NarrationStarted;   // string = poi.Name (AudioViewModel dùng)
    public event Action<string>? NarrationStartedWithLang; // string = langCode (PoiDetail dùng)
    public event Action? NarrationFinished;
    /// <summary>
    /// Fire định kỳ khi audio đang phát: (currentPosition seconds, totalDuration seconds).
    /// Duration lấy trực tiếp từ metadata file audio.
    /// </summary>
    public event Action<double, double>? PlaybackPositionChanged;
    private LocalPoi? _currentPoi;
    public bool IsPlaying => _isPlaying;
    public bool IsPaused => _isPaused;

    public NarrationEngine(AppDatabase db, AudioPlayerService audioPlayer)
    {
        _db = db;
        _audioPlayer = audioPlayer;
        _audioPlayer.PlaybackPositionChanged += (pos, dur) =>
            PlaybackPositionChanged?.Invoke(pos, dur);
    }

    /// <summary>
    /// Phát audio cho POI — priority:
    /// 1. Audio file đã tải local → phát ngay (offline OK)
    /// 2. Audio file chưa tải → tải từ protected endpoint → phát
    /// 3. Không tải được → TTS fallback
    /// </summary>
    public async Task PlayAsync(LocalPoi poi, double distanceMeters, Location userLocation,
        string triggerType = "geofence_proximity", TimeSpan? startPosition = null)
    {
        await _lock.WaitAsync();
        _playbackCts = new CancellationTokenSource();
        try
        {
            var previousPoi = _currentPoi;
            _currentPoi = poi;

            // Đang pause cùng POI → chỉ resume, không phát lại từ đầu
            if (_isPaused && previousPoi != null && previousPoi.Id == poi.Id && startPosition == null)
            {
                _audioPlayer.Resume();
                _isPlaying = true;
                _isPaused = false;
                return;
            }

            if (_isPlaying && !_isPaused) return;
            _isPlaying = true;
            _isPaused = false;

            var script = await _db.GetAudioScriptAsync(poi.Id, CurrentLanguage)
                      ?? await _db.GetAudioScriptAsync(poi.Id, "vi");

            if (script == null) return;

            var usedLang = script.LanguageCode;
            NarrationStarted?.Invoke(poi.Name);
            NarrationStartedWithLang?.Invoke(usedLang);

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

            // 1. Audio file đã tải local → phát ngay (offline OK)
            if (script.IsAudioDownloaded && !string.IsNullOrEmpty(script.LocalAudioPath)
                && File.Exists(script.LocalAudioPath))
            {
                try
                {
                    await _audioPlayer.PlayAudioAsync(script, _playbackCts.Token, startPosition);
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch { /* File lỗi → xóa cache */ }
            }

            // 2. Thử tải audio từ protected endpoint
            if (!string.IsNullOrWhiteSpace(script.TtsScript))
            {
                try
                {
                    await _audioPlayer.PlayAudioAsync(script, _playbackCts.Token, startPosition);
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch { /* Tải lỗi → TTS fallback */ }
            }

            // 3. TTS fallback — dùng device TTS
            await SpeakWithTtsAsync(script, _playbackCts.Token);
        }
        finally
        {
            if (!_isPaused)
            {
                _playbackCts = null;
                _isPlaying = false;
                _currentPoi = null;
            }
            _lock.Release();
            if (!_isPaused)
                NarrationFinished?.Invoke();
        }
    }

    /// <summary>
    /// Dừng audio đang phát ngay lập tức (TTS hoặc file).
    /// </summary>
    public void Stop()
    {
        _ttsCts?.Cancel();
        _playbackCts?.Cancel();
        _audioPlayer.Stop();
        _isPlaying = false;
        _isPaused = false;
        _currentPoi = null;
        try { NarrationFinished?.Invoke(); } catch { /* ignore */ }
    }

    /// <summary>
    /// Tạm dừng audio — GIỮ player để Resume đúng vị trí.
    /// Trả về vị trí đã phát được (để hiển thị progress).
    /// </summary>
    public TimeSpan Pause()
    {
        if (!_isPlaying) return TimeSpan.Zero;
        _isPaused = true;
        _isPlaying = false;
        var pos = _audioPlayer.GetCurrentPosition();
        _audioPlayer.Pause();
        return pos;
    }

    /// <summary>
    /// Tiếp tục phát từ vị trí đã Pause.
    /// </summary>
    public void Resume()
    {
        _audioPlayer.Resume();
        _isPaused = false;
        _isPlaying = true;
    }

    private async Task SpeakWithTtsAsync(LocalAudioScript script, CancellationToken ct = default)
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
        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ttsCts = localCts;
        try
        {
            await TextToSpeech.SpeakAsync(script.TtsScript, new SpeechOptions
            {
                Locale = mauiLocale,
                Volume = 1.0f,
                Pitch = 1.0f
            }, _ttsCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Bị dừng — không throw
        }
        finally
        {
            _ttsCts = null;
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
