using Foundation;
using TrackHubMobile.Services.Interfaces;
using UIKit;

namespace TrackHubMobile;

[Register(nameof(AppDelegate))]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        var uri = new Uri(url.AbsoluteString);

        // First, try handling login callback
        if (WebAuthenticator.Default.OpenUrl(uri))
            return true;

        // Then, check for logout callback
        if (uri.Scheme == Utils.Constants.LogoutScheme && uri.Host == Utils.Constants.LogoutHost)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var authService = IPlatformApplication.Current?.Services.GetService<IAuthenticationService>();
                if (authService != null)
                {
                    await authService.LoginAsync();
                }
            });

            return true;
        }

        return false;
    }
}
