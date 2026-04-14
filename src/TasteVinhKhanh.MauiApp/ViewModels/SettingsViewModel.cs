using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly NarrationEngine _narration;
    private readonly LocalizationService _i18n;

    [ObservableProperty] private bool _autoPlayAudio;
    [ObservableProperty] private bool _highContrast;
    [ObservableProperty] private double _textSize = 2;
    [ObservableProperty] private string _selectedLanguage = "vi";

    // ── Bindable translated strings ──
    [ObservableProperty] private string _tHeader = "";
    [ObservableProperty] private string _tSubtitle = "";
    [ObservableProperty] private string _tLanguage = "";
    [ObservableProperty] private string _tNative = "";
    [ObservableProperty] private string _tInternational = "";
    [ObservableProperty] private string _tChineseSimplified = "";
    [ObservableProperty] private string _tKorean = "";
    [ObservableProperty] private string _tJapanese = "";
    [ObservableProperty] private string _tAudioFeatures = "";
    [ObservableProperty] private string _tAutoPlay = "";
    [ObservableProperty] private string _tAutoPlayHint = "";
    [ObservableProperty] private string _tDisplay = "";
    [ObservableProperty] private string _tTextSizeSmall = "";
    [ObservableProperty] private string _tTextSizeLarge = "";
    [ObservableProperty] private string _tContrast = "";
    [ObservableProperty] private string _tHelp = "";
    [ObservableProperty] private string _tHelpHint = "";
    [ObservableProperty] private string _tVersion = "";
    [ObservableProperty] private string _tVersionSub = "";
    [ObservableProperty] private string _tNavHome = "";
    [ObservableProperty] private string _tNavMap = "";
    [ObservableProperty] private string _tNavAudio = "";
    [ObservableProperty] private string _tNavFavorites = "";
    [ObservableProperty] private string _tNavSettings = "";

    public SettingsViewModel(NarrationEngine narration, LocalizationService i18n)
    {
        _narration = narration;
        _i18n = i18n;
        LoadSettings();
        RefreshTexts();

        // Cập nhật text khi ngôn ngữ thay đổi (từ Settings hoặc chỗ khác)
        _i18n.LanguageChanged += RefreshTexts;
    }

    private void LoadSettings()
    {
        AutoPlayAudio = Preferences.Get("auto_play", true);
        HighContrast = Preferences.Get("high_contrast", false);
        TextSize = Preferences.Get("text_size", 2.0);
        SelectedLanguage = Preferences.Get("language", "vi");
    }

    private void RefreshTexts()
    {
        THeader = _i18n.T("Settings_Header");
        TSubtitle = _i18n.T("Settings_Subtitle");
        TLanguage = _i18n.T("Settings_Language");
        TNative = _i18n.T("Settings_Native");
        TInternational = _i18n.T("Settings_International");
        TChineseSimplified = _i18n.T("Settings_ChineseSimplified");
        TKorean = _i18n.T("Settings_Korean");
        TJapanese = _i18n.T("Settings_Japanese");
        TAudioFeatures = _i18n.T("Settings_AudioFeatures");
        TAutoPlay = _i18n.T("Settings_AutoPlay");
        TAutoPlayHint = _i18n.T("Settings_AutoPlayHint");
        TDisplay = _i18n.T("Settings_Display");
        TTextSizeSmall = _i18n.T("Settings_TextSizeSmall");
        TTextSizeLarge = _i18n.T("Settings_TextSizeLarge");
        TContrast = _i18n.T("Settings_Contrast");
            THelp = _i18n.T("Settings_Help");
        THelpHint = _i18n.T("Settings_HelpHint");
        TVersion = _i18n.T("Settings_Version");
        TVersionSub = _i18n.T("Settings_VersionSub");
        TNavHome = _i18n.T("Nav_Home");
        TNavMap = _i18n.T("Nav_Map");
        TNavAudio = _i18n.T("Nav_Audio");
        TNavFavorites = _i18n.T("Nav_Favorites");
        TNavSettings = _i18n.T("Nav_Settings");
    }

    partial void OnAutoPlayAudioChanged(bool value)
        => Preferences.Set("auto_play", value);

    partial void OnHighContrastChanged(bool value)
        => Preferences.Set("high_contrast", value);

    partial void OnTextSizeChanged(double value)
        => Preferences.Set("text_size", value);

    [RelayCommand]
    public void SelectLanguage(string lang)
    {
        SelectedLanguage = lang;
        _i18n.SetLanguage(lang);
        _narration.CurrentLanguage = lang;
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
}
