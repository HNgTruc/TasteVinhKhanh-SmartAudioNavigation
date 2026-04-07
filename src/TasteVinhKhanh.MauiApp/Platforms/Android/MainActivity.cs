using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

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

        // Set status bar color — use modern API on Android 30+
        // Falls back to legacy API (with pragma suppress) on older versions
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#1A0A00"));
        }
        else
        {
#pragma warning disable CA1416 // Platform compatibility check already done via SdkInt check
            var window = Window;
            if (window != null)
            {
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)
                    (SystemUiFlags.LayoutStable | SystemUiFlags.LayoutFullscreen);
                window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#1A0A00"));
            }
#pragma warning restore CA1416
        }
    }
}