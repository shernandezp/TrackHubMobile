using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace TrackHubMobile;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = Utils.Constants.LogoutScheme,
    DataHost = Utils.Constants.LogoutHost)]
public class LogoutCallbackActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Compared by scheme + host, not as a whole string: the identity server redirects
        // to the normalized URI (trackhubmobile://logoutcallback/), so an exact match fails.
        var data = Intent?.Data;

        if (data is not null &&
            string.Equals(data.Scheme, Utils.Constants.LogoutScheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(data.Host, Utils.Constants.LogoutHost, StringComparison.OrdinalIgnoreCase))
        {
            // Bring the app back to the front and let the app lifecycle ask for a new
            // sign-in: App.OnResume owns that decision, and it also covers the case where
            // Android reclaimed the process while the logout browser was open.
            // ClearTop + SingleTop reuses the running MainActivity instead of stacking one.
            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            StartActivity(intent);
        }

        Finish();
    }
}
