using TasteVinhKhanh.MauiApp.ViewModels;

namespace TasteVinhKhanh.MauiApp.Views;

public partial class AudioPage : ContentPage
{
    public AudioPage(AudioViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}