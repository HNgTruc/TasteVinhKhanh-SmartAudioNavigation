using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;
using Location = Microsoft.Maui.Devices.Sensors.Location;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class AudioViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;
    private readonly LocalizationService _i18n;

    [ObservableProperty] private string _nowPlayingName = "";
    [ObservableProperty] private string _nowPlayingDescription = "";
    [ObservableProperty] private string _nowPlayingStallLabel = "";
    [ObservableProperty] private string _playPauseIcon = "▶";
    [ObservableProperty] private bool _isPlaying = false;
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private string _currentTime = "00:00";
    [ObservableProperty] private string _totalTime = "00:00";
    [ObservableProperty] private string _speedLabel = "1.0x";
    [ObservableProperty] private string _volumeLabel = "80%";

    // ── Bindable translated strings ──
    [ObservableProperty] private string _tNowPlaying = "";
    [ObservableProperty] private string _tSelect = "";
    [ObservableProperty] private string _tSelectHint = "";
    [ObservableProperty] private string _tStall = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavFavorites = "";
    [ObservableProperty] private string _tNavSettings = "";

    private LocalPoi? _currentPoi;
    private TimeSpan _pausedPosition = TimeSpan.Zero;

    private static readonly string LastPoiIdKey = "last_played_poi_id";

    public AudioViewModel(AppDatabase db, NarrationEngine narration, LocalizationService i18n)
    {
        _db = db;
        _narration = narration;
        _i18n = i18n;
        RefreshTexts();

        _i18n.LanguageChanged += RefreshTexts;

        _narration.NarrationStarted += OnNarrationStarted;
        _narration.NarrationFinished += OnNarrationFinished;
        _narration.PlaybackPositionChanged += OnPlaybackPositionChanged;

        _ = RestoreLastPlayedAsync();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        _geofence?.SetGeofenceBlocked(value);
    }

    private void OnPlaybackPositionChanged(double positionSec, double durationSec)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (positionSec >= 0)
            {
                var pos = TimeSpan.FromSeconds(positionSec);
                CurrentTime = $"{pos.Minutes:D2}:{pos.Seconds:D2}";
            }

            if (durationSec > 0)
            {
                var dur = TimeSpan.FromSeconds(durationSec);
                TotalTime = $"{dur.Minutes:D2}:{dur.Seconds:D2}";
                Progress = positionSec / durationSec;
            }
        });
    }

    private void RefreshTexts()
    {
        TNowPlaying = _i18n.T("Audio_NowPlaying");
        TSelect = _i18n.T("Audio_Select");
        TSelectHint = _i18n.T("Audio_SelectHint");
        TStall = _i18n.T("Audio_Stall");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavFavorites = _i18n.T("Nav_Favorites");
        TNavSettings = _i18n.T("Nav_Settings");
    }

    private void OnNarrationStarted(string poiName)
    {
        // _poisCache đã được populate bởi RestoreLastPlayedAsync khi app khởi động
        // Dùng SynchronizationContext.Current để check thread
        if (_poisCache == null)
        {
            // Cache chưa sẵn sàng → bỏ qua, AudioPage đã restore từ Preferences rồi
            return;
        }

        _currentPoi = _poisCache.FirstOrDefault(p => p.Name == poiName);
        if (_currentPoi == null) return;

        NowPlayingName = _currentPoi.Name;
        NowPlayingDescription = _currentPoi.ShortDescription;
        NowPlayingStallLabel = $"{TStall}{_currentPoi.Id:D2}";
        PlayPauseIcon = "⏸";
        IsPlaying = true;
        Preferences.Set(LastPoiIdKey, _currentPoi.Id);
    }

    private void OnNarrationFinished()
    {
        if (!IsPlaying && PlayPauseIcon == "▶") return;
        PlayPauseIcon = "▶";
        IsPlaying = false;
        _geofence?.SetGeofenceBlocked(false);
    }

    private List<LocalPoi> _poisCache = null;

    private async Task RestoreLastPlayedAsync()
    {
        await _db.InitAsync();
        var all = await _db.GetAllPoisAsync();
        _poisCache = all;

        var lastId = Preferences.Get(LastPoiIdKey, -1);
        if (lastId <= 0) return;

        var lastPoi = all.FirstOrDefault(p => p.Id == lastId);
        if (lastPoi == null) return;

        _currentPoi = lastPoi;
        NowPlayingName = lastPoi.Name;
        NowPlayingDescription = lastPoi.ShortDescription;
        NowPlayingStallLabel = $"{TStall}{lastPoi.Id:D2}";
        // Chưa phát → icon ▶, IsPlaying = false
    }

    [RelayCommand]
    public void PlayPause()
    {
        if (IsPlaying)
        {
            // Tạm dừng — giữ nguyên player để resume đúng vị trí
            _pausedPosition = _narration.Pause();
            PlayPauseIcon = "▶";
            IsPlaying = false;
        }
        else
        {
            if (_currentPoi == null) return;
            if (_pausedPosition > TimeSpan.Zero)
            {
                _narration.Resume();
                PlayPauseIcon = "⏸";
                IsPlaying = true;
                return;
            }
            // Phát lại từ đúng vị trí đã Pause, không phát lại từ đầu
            _ = _narration.PlayAsync(_currentPoi, 0,
                new Location(10.7629, 106.6604), "manual", _pausedPosition);
            PlayPauseIcon = "⏸";
            IsPlaying = true;
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _narration.Stop();
        _geofence?.SetGeofenceBlocked(false); // mở lại geofence
        _pausedPosition = TimeSpan.Zero;
        Progress = 0;
        CurrentTime = "00:00";
        PlayPauseIcon = "▶";
        IsPlaying = false;
    }

    [RelayCommand]
    public async Task GoToHome()
        => await Shell.Current.GoToAsync("//main");

    [RelayCommand]
    public async Task GoToMap()
        => await Shell.Current.GoToAsync("//map");

    [RelayCommand]
    public async Task GoToAudio()
        => await Shell.Current.GoToAsync("//audio");

    [RelayCommand]
    public async Task GoToSettings()
        => await Shell.Current.GoToAsync("//settings");

    [RelayCommand]
    public async Task GoToFavorites()
        => await Shell.Current.GoToAsync("//favorites");

    // ── External call from AudioPage: bắt đầu phát thủ công 1 POI ──
    public async Task PlayPoiAsync(LocalPoi poi)
    {
        _currentPoi = poi;
        NowPlayingName = poi.Name;
        NowPlayingDescription = poi.ShortDescription;
        NowPlayingStallLabel = $"{TStall}{poi.Id:D2}";

        await _narration.PlayAsync(poi, 0,
            new Location(10.7629, 106.6604), "manual", null);
        PlayPauseIcon = "⏸";
        IsPlaying = true;
        Preferences.Set(LastPoiIdKey, poi.Id);
    }

    // ── Inject GeofenceEngine để block khi nghe thủ công ──
    private GeofenceEngine? _geofence;
    public void SetGeofenceEngine(GeofenceEngine geofence) => _geofence = geofence;
}
