using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace TrackHubMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The OS snapshots the foreground screen on every background transition and keeps it on
        // disk for the recents preview. This app's screens carry fleet positions, and its login
        // screen can have the password revealed.
        Window?.AddFlags(WindowManagerFlags.Secure);
    }
}
