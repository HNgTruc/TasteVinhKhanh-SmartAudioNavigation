using CommunityToolkit.Maui;
using Plugin.LocalNotification;
using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;
using TasteVinhKhanh.MauiApp.Views;

namespace TasteVinhKhanh.MauiApp;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseMauiCommunityToolkit()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // ── DATABASE ──────────────────────────────────────────
        builder.Services.AddSingleton<AppDatabase>();

        // ── HTTP CLIENT → API ─────────────────────────────────
        // Dùng URL cấu hình chung để chạy được cả emulator, máy thật và bản APK phát hành.
        var apiBaseUrl = ApiConfig.GetApiBaseUrl();

        // AddHttpClient injects HttpClient vào SyncService constructor
        builder.Services.AddHttpClient<SyncService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // AudioPlayerService cũng cần HttpClient để tải audio từ protected endpoint
        builder.Services.AddHttpClient<AudioPlayerService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── SERVICES ──────────────────────────────────────────
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<LocationService>();
        // AudioPlayerService được đăng ký bởi AddHttpClient<AudioPlayerService> ở trên
        builder.Services.AddSingleton<NarrationEngine>();
        builder.Services.AddSingleton<GeofenceEngine>();
        builder.Services.AddSingleton<LocalizationService>();

        // ViewModels
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<MapViewModel>();
        builder.Services.AddTransient<AudioViewModel>();
        builder.Services.AddTransient<ToursViewModel>();
        builder.Services.AddTransient<LanguageOnboardingViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<PoiDetailViewModel>();
        builder.Services.AddTransient<FavoritesViewModel>();

        // Views
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<AudioPage>();
        builder.Services.AddTransient<ToursPage>();
        builder.Services.AddTransient<LanguageOnboardingPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<PoiDetailPage>();
        builder.Services.AddTransient<FavoritesPage>();

        builder.Services.AddHttpClient<ToursViewModel>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return builder.Build();
    }
}
