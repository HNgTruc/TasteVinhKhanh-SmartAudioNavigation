using TasteVinhKhanh.MauiApp.Data;
using TasteVinhKhanh.MauiApp.Services;
using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private readonly SyncService _sync;

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
        await _vm.InitAsync();
        await _sync.SyncPoisAsync();
        await _vm.InitAsync();
    }

    private async void OnPoiCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is LocalPoi poi)
            await _vm.GoToDetail(poi.Id.ToString());
    }
}