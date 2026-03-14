using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class PoiDetailPage : ContentPage
{
    public PoiDetailPage(PoiDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}