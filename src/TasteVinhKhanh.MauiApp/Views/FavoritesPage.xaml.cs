using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class FavoritesPage : ContentPage
{
    public FavoritesPage(FavoritesViewModel vm)
    {
        BindingContext = vm;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FavoritesViewModel vm)
            await vm.InitAsync();
    }

    private async void OnPoiCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is Element element && element.BindingContext is Data.LocalPoi poi)
            await Shell.Current.GoToAsync($"PoiDetailPage?poiId={poi.Id}");
    }
}
