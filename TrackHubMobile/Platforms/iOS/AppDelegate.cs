using Foundation;
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

        // Then, check for logout callback. Returning to the app is all that is needed here:
        // the app lifecycle (App.OnResume, or MainPage on a cold start) owns the decision to
        // ask for a new sign-in, so there is a single place that opens the browser.
        if (uri.Scheme == Utils.Constants.LogoutScheme && uri.Host == Utils.Constants.LogoutHost)
        {
            return true;
        }

        return false;
    }
}
