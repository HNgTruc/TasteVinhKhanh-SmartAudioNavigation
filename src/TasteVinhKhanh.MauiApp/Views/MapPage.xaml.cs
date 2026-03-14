using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;

    public MapPage(MapViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }
}