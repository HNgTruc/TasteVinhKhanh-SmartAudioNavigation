using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

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
    [ObservableProperty] private string _tNavSettings = "";

    private LocalPoi? _currentPoi;

    public AudioViewModel(AppDatabase db, NarrationEngine narration, LocalizationService i18n)
    {
        _db = db;
        _narration = narration;
        _i18n = i18n;
        RefreshTexts();

        _i18n.LanguageChanged += RefreshTexts;

        _narration.NarrationStarted += OnNarrationStarted;
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
        TNavSettings = _i18n.T("Nav_Settings");
    }

    private async void OnNarrationStarted(string poiName)
    {
        var pois = await _db.GetAllPoisAsync();
        _currentPoi = pois.FirstOrDefault(p => p.Name == poiName);
        if (_currentPoi == null) return;

        NowPlayingName = _currentPoi.Name;
        NowPlayingDescription = _currentPoi.ShortDescription;
        NowPlayingStallLabel = $"{TStall}{_currentPoi.Id:D2}";
        PlayPauseIcon = "⏸";
        IsPlaying = true;
    }

    [RelayCommand]
    public async Task PlayPause()
    {
        if (_currentPoi == null) return;

        if (IsPlaying)
        {
            PlayPauseIcon = "▶";
            IsPlaying = false;
        }
        else
        {
            PlayPauseIcon = "⏸";
            IsPlaying = true;
        }
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
}
