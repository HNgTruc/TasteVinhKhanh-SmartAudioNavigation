using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class AudioPage : ContentPage
{
    public AudioPage(AudioViewModel vm, GeofenceEngine geofence)
    {
        InitializeComponent();
        vm.SetGeofenceEngine(geofence);
        BindingContext = vm;
    }
}
