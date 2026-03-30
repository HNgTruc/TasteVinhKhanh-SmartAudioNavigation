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

    [ObservableProperty] private List<LocalPoi> _topPois = new();
    [ObservableProperty] private string _currentLanguageLabel = "🇻🇳 VI";

    private static readonly Dictionary<string, string> _langLabels = new()
    {
        ["vi"] = "🇻🇳 VI",
        ["en"] = "🇬🇧 EN",
        ["zh"] = "🇨🇳 ZH",
        ["ko"] = "🇰🇷 KO",
        ["ja"] = "🇯🇵 JA",
    };

    public HomeViewModel(AppDatabase db, SyncService sync, NarrationEngine narration)
    {
        _db = db;
        _sync = sync;
        _narration = narration;
        var currentLang = Preferences.Get("language", "vi");
        CurrentLanguageLabel = _langLabels.GetValueOrDefault(currentLang, "🇻🇳 VI");
    }

    [RelayCommand]
    public async Task InitAsync()
    {
        await _db.InitAsync();
        var all = await _db.GetAllPoisAsync();
        TopPois = all.OrderByDescending(p => p.Priority).Take(5).ToList();
    }

    [RelayCommand]
    public async Task OpenLanguagePicker()
    {
        string? selected = await Shell.Current.DisplayActionSheet(
            "Chọn ngôn ngữ",
            "Huỷ",
            null,
            "🇻🇳 Tiếng Việt",
            "🇬🇧 English",
            "🇨🇳 中文",
            "🇰🇷 한국어",
            "🇯🇵 日本語");

        if (string.IsNullOrEmpty(selected) || selected == "Huỷ") return;

        // Cập nhật label
        CurrentLanguageLabel = selected switch
        {
            "🇻🇳 Tiếng Việt" => "🇻🇳 VI",
            "🇬🇧 English" => "🇬🇧 EN",
            "🇨🇳 中文" => "🇨🇳 ZH",
            "🇰🇷 한국어" => "🇰🇷 KO",
            "🇯🇵 日本語" => "🇯🇵 JA",
            _ => CurrentLanguageLabel
        };

        // Lưu và cập nhật ngôn ngữ hiện tại
        var langCode = selected switch
        {
            "🇻🇳 Tiếng Việt" => "vi",
            "🇬🇧 English" => "en",
            "🇨🇳 中文" => "zh",
            "🇰🇷 한국어" => "ko",
            "🇯🇵 日本語" => "ja",
            _ => "vi"
        };
        Preferences.Set("language", langCode);
        _narration.CurrentLanguage = langCode;
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
