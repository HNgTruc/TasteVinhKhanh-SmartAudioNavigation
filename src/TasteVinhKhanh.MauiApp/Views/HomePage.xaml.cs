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
        // Init DB + load local data trước
        await _vm.InitAsync();
        // Sync với server để lấy dữ liệu POI + audio script mới nhất
        await _sync.SyncPoisAsync();
        // Reload lại sau sync
        await _vm.InitAsync();
    }
}