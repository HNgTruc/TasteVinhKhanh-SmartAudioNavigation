using TasteVinhKhanh.MauiApp.Views;

namespace TasteVinhKhanh.MauiApp;

public partial class AppShell : Shell
{
    public static event Action? FavoriteChanged;

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("PoiDetailPage", typeof(PoiDetailPage));
        Routing.RegisterRoute("tours", typeof(ToursPage));
    }

    /// <summary>Call this from outside AppShell to fire the FavoriteChanged event.</summary>
    public static void NotifyFavoriteChanged() => FavoriteChanged?.Invoke();
}
