using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class AudioViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;

    [ObservableProperty] private string _nowPlayingName = "Chọn một điểm thuyết minh";
    [ObservableProperty] private string _nowPlayingDescription = "Đi đến bản đồ và chọn một quán để bắt đầu nghe thuyết minh";
    [ObservableProperty] private string _nowPlayingStallLabel = "STALL #--";
    [ObservableProperty] private string _playPauseIcon = "▶";
    [ObservableProperty] private bool _isPlaying = false;
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private string _currentTime = "00:00";
    [ObservableProperty] private string _totalTime = "00:00";
    [ObservableProperty] private string _speedLabel = "1.0x";
    [ObservableProperty] private string _volumeLabel = "80%";

    private LocalPoi? _currentPoi;

    public AudioViewModel(AppDatabase db, NarrationEngine narration)
    {
        _db = db;
        _narration = narration;

        _narration.NarrationStarted += OnNarrationStarted;
    }

    private async void OnNarrationStarted(string poiName)
    {
        var pois = await _db.GetAllPoisAsync();
        _currentPoi = pois.FirstOrDefault(p => p.Name == poiName);
        if (_currentPoi == null) return;

        NowPlayingName = _currentPoi.Name;
        NowPlayingDescription = _currentPoi.ShortDescription;
        NowPlayingStallLabel = $"STALL #{_currentPoi.Id:D2}";
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
    => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    public async Task GoToMap()
        => await Shell.Current.GoToAsync("//MapPage");

    [RelayCommand]
    public async Task GoToAudio()
        => await Shell.Current.GoToAsync("//AudioPage");

    [RelayCommand]
    public async Task GoToSettings()
        => await Shell.Current.GoToAsync("//SettingsPage");
}