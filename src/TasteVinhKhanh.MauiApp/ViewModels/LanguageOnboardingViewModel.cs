using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp.ViewModels;

public partial class LanguageOnboardingViewModel : ObservableObject
{
    private readonly LocalizationService _i18n;
    private readonly NarrationEngine _narration;

    [ObservableProperty] private string _selectedLanguage = "en";

    public LanguageOnboardingViewModel(LocalizationService i18n, NarrationEngine narration)
    {
        _i18n = i18n;
        _narration = narration;
    }

    [RelayCommand]
    public void SelectLanguage(string lang)
    {
        SelectedLanguage = lang;
    }

    [RelayCommand]
    public async Task ContinueAsync()
    {
        ApplyLanguage(SelectedLanguage);
        await Shell.Current.Navigation.PopModalAsync(true);
    }

    [RelayCommand]
    public async Task UseSystemLanguageAsync()
    {
        var systemLang = _i18n.GetBestSupportedSystemLanguage();
        ApplyLanguage(systemLang);
        await Shell.Current.Navigation.PopModalAsync(true);
    }

    private void ApplyLanguage(string lang)
    {
        _i18n.SetLanguage(lang);
        _narration.CurrentLanguage = lang;
        Preferences.Set("language_onboarding_done", true);
    }
}
