using Android.App;
using Android.Content.PM;
using Android.Views;
using Android.OS;

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
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window.SetDecorFitsSystemWindows(false);
            Window.StatusBarColor = Android.Graphics.Color.Transparent;
        }
    }
}