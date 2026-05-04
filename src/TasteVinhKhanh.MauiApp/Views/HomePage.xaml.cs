using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private readonly SyncService _sync;
    private bool _loadedOnce;

    public HomePage(HomeViewModel vm, SyncService sync)
    {
        InitializeComponent();
        _vm = vm;
        _sync = sync;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loadedOnce)
        {
            _loadedOnce = true;
            await _vm.InitAsync();
            _ = _sync.UploadActiveHeartbeatAsync();
            _ = Task.Run(async () =>
            {
                await _sync.SyncPoisAsync();
                await MainThread.InvokeOnMainThreadAsync(async () => await _vm.InitAsync());
            });
            return;
        }

        await _vm.InitAsync();
        _ = _sync.SyncPoisAsync();
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            await MainThread.InvokeOnMainThreadAsync(async () => await _vm.InitAsync());
        });
    }

    private async void OnPoiCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is LocalPoi poi)
            await _vm.GoToDetail(poi.Id.ToString());
    }
}
