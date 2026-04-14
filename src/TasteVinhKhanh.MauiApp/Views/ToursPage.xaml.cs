using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class ToursPage : ContentPage
{
    private readonly ToursViewModel _vm;
    private bool _loadingStarted;

    public ToursPage(ToursViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loadingStarted) return;
        _loadingStarted = true;
        await _vm.InitAsync();
    }
}
