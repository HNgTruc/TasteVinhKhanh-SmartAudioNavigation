using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class LanguageOnboardingPage : ContentPage
{
    public LanguageOnboardingPage(LanguageOnboardingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
