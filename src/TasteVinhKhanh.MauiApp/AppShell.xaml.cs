using TasteVinhKhanh.MauiApp.Views;

namespace TasteVinhKhanh.MauiApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("PoiDetailPage", typeof(PoiDetailPage));
    }
}