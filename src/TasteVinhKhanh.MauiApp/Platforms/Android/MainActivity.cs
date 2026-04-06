using Android.App;
using Android.Content.PM;
using Android.Views;
using Android.OS;
using Microsoft.Maui;

namespace TasteVinhKhanh.MauiApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var window = Platform.CurrentActivity?.Window;
        if (window != null)
        {
            window.DecorView.SystemUiVisibility = (StatusBarVisibility)
                (SystemUiFlags.LayoutStable | SystemUiFlags.LayoutFullscreen);
            window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#1A0A00"));
        }
    }
}