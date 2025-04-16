using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TrackHubMobile.Services.Interfaces;

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

        var data = Intent?.Data?.ToString();

        if (!string.IsNullOrEmpty(data) && data.Equals(Utils.Constants.LogoutCallbackUrl))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var authService = IPlatformApplication.Current?.Services.GetService<IAuthenticationService>();
                if (authService != null)
                {
                    await authService.LoginAsync();
                }
            });
        }

        Finish();
    }
}
