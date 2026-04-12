using Microsoft.Extensions.DependencyInjection;
using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class PoiDetailPage : ContentPage
{
    private readonly LocalizationService _i18n;
    private readonly NarrationEngine _narration;

    // Constructor nhận đủ services từ DI
    public PoiDetailPage(
        PoiDetailViewModel vm,
        LocalizationService i18n,
        NarrationEngine narration)
    {
        InitializeComponent();
        BindingContext = vm;
        _i18n = i18n;
        _narration = narration;
    }

    public PoiDetailPage()
    {
        InitializeComponent();
        var services = MauiProgram.CreateMauiApp().Services;
        _i18n = services.GetRequiredService<LocalizationService>();
        _narration = services.GetRequiredService<NarrationEngine>();
        BindingContext = services.GetRequiredService<PoiDetailViewModel>();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnTabLocationTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is PoiDetailViewModel vm)
            vm.SelectTabCommand.Execute(0);
    }

    private void OnTabNarrationTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is PoiDetailViewModel vm)
            vm.SelectTabCommand.Execute(1);
    }

    private void OnTabFavoriteTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is PoiDetailViewModel vm)
            vm.ToggleFavoriteCommand.Execute(null);
    }

    private void OnLangChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is Data.LocalAudioScript script)
        {
            _narration.CurrentLanguage = script.LanguageCode;
            if (BindingContext is PoiDetailViewModel vm)
                vm.SafeSetAudioLang(script.LanguageCode);
        }
    }
}
