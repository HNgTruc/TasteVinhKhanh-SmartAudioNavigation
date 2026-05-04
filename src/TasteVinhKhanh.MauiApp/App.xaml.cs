using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;
using TasteVinhKhanh.MauiApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace TasteVinhKhanh.MauiApp;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        var shell = _services.GetRequiredService<AppShell>();
        var window = new Microsoft.Maui.Controls.Window(shell);

        var picked = Preferences.Get("language_onboarding_done", false);
        if (!picked)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var vm = _services.GetRequiredService<LanguageOnboardingViewModel>();
                await shell.Navigation.PushModalAsync(new LanguageOnboardingPage(vm), true);
            });
        }

        return window;
    }
}
