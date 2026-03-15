using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly NarrationEngine _narration;

    [ObservableProperty] private bool _autoPlayAudio;
    [ObservableProperty] private bool _highContrast;
    [ObservableProperty] private double _textSize = 2;
    [ObservableProperty] private string _selectedLanguage = "en";

    public SettingsViewModel(NarrationEngine narration)
    {
        _narration = narration;
        LoadSettings();
    }

    private void LoadSettings()
    {
        AutoPlayAudio = Preferences.Get("auto_play", true);
        HighContrast = Preferences.Get("high_contrast", false);
        TextSize = Preferences.Get("text_size", 2.0);
        SelectedLanguage = Preferences.Get("language", "vi");
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
        _narration.CurrentLanguage = lang;
        Preferences.Set("language", lang);
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