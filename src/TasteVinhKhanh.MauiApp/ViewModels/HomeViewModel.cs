using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly SyncService _sync;
    private readonly NarrationEngine _narration;
    private readonly LocalizationService _i18n;

    [ObservableProperty] private List<LocalPoi> _topPois = new();

    // ── Bindable translated strings ──
    [ObservableProperty] private string _tWelcome = "";
    [ObservableProperty] private string _tTitleLine1 = "";
    [ObservableProperty] private string _tTitleLine2 = "";
    [ObservableProperty] private string _tSubtitle = "";
    [ObservableProperty] private string _tStreetEats = "";
    [ObservableProperty] private string _tStreetEatsSub = "";
    [ObservableProperty] private string _tExplore = "";
    [ObservableProperty] private string _tMap = "";
    [ObservableProperty] private string _tMapSub = "";
    [ObservableProperty] private string _tAudio = "";
    [ObservableProperty] private string _tAudioSub = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavSettings = "";

    public HomeViewModel(AppDatabase db, SyncService sync,
        NarrationEngine narration, LocalizationService i18n)
    {
        _db = db;
        _sync = sync;
        _narration = narration;
        _i18n = i18n;
        RefreshTexts();

        // Cập nhật text khi ngôn ngữ thay đổi
        _i18n.LanguageChanged += RefreshTexts;
    }

    private void RefreshTexts()
    {
        TWelcome = _i18n.T("Home_Welcome");
        TTitleLine1 = _i18n.T("Home_TitleLine1");
        TTitleLine2 = _i18n.T("Home_TitleLine2");
        TSubtitle = _i18n.T("Home_Subtitle");
        TStreetEats = _i18n.T("Home_StreetEats");
        TStreetEatsSub = _i18n.T("Home_StreetEatsSub");
        TExplore = _i18n.T("Home_Explore");
        TMap = _i18n.T("Home_Map");
        TMapSub = _i18n.T("Home_MapSub");
        TAudio = _i18n.T("Home_Audio");
        TAudioSub = _i18n.T("Home_AudioSub");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavSettings = _i18n.T("Nav_Settings");
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
