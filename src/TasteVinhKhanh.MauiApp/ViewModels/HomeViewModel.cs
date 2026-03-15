using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly SyncService _sync;

    [ObservableProperty] private List<LocalPoi> _topPois = new();
    [ObservableProperty] private string _currentLanguageLabel = "🇻🇳 VI | 🇬🇧 EN";

    public HomeViewModel(AppDatabase db, SyncService sync)
    {
        _db = db;
        _sync = sync;
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        await _db.InitAsync();
        var all = await _db.GetAllPoisAsync();
        TopPois = all.OrderByDescending(p => p.Priority).Take(5).ToList();
    }

    [RelayCommand]
    public async Task GoToDetail(int poiId)
        => await Shell.Current.GoToAsync($"PoiDetailPage?poiId={poiId}");

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