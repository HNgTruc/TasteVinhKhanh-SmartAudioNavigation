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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // ── DATABASE ──────────────────────────────────────────
        builder.Services.AddSingleton<AppDatabase>();

        // ── HTTP CLIENT → API ─────────────────────────────────
        // Android emulator: 10.0.2.2 trỏ về localhost máy host
        // iOS simulator:    localhost trỏ thẳng
        var apiBaseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000/"
            : "http://localhost:5000/";

        builder.Services.AddHttpClient<SyncService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── SERVICES ──────────────────────────────────────────
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<GeofenceEngine>();
        builder.Services.AddSingleton<NarrationEngine>();

        // ── VIEWMODELS ────────────────────────────────────────
        builder.Services.AddTransient<MapViewModel>();
        builder.Services.AddTransient<PoiDetailViewModel>();

        // ── VIEWS ─────────────────────────────────────────────
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<PoiDetailPage>();

        return builder.Build();
    }
}