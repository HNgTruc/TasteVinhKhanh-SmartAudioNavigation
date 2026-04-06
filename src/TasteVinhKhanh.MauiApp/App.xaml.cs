namespace TasteVinhKhanh.MauiApp;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        return new Microsoft.Maui.Controls.Window(new AppShell());
    }
}
