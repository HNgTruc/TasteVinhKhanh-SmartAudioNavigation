using TasteVinhKhanh.MauiApp.Views;
using TasteVinhKhanh.MauiApp.Services;

namespace TasteVinhKhanh.MauiApp;

public partial class AppShell : Shell
{
    public static event Action? FavoriteChanged;
    private readonly SyncService _sync;

    public AppShell(SyncService sync)
    {
        InitializeComponent();
        _sync = sync;
        Routing.RegisterRoute("PoiDetailPage", typeof(PoiDetailPage));
        Routing.RegisterRoute("tours", typeof(ToursPage));
    }

    /// <summary>Call this from outside AppShell to fire the FavoriteChanged event.</summary>
    public static void NotifyFavoriteChanged() => FavoriteChanged?.Invoke();

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        _ = _sync.UploadActiveHeartbeatAsync();
    }
}
