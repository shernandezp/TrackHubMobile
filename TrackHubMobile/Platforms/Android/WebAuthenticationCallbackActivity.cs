using Android.App;
using Android.Content;
using Android.Content.PM;

namespace TrackHubMobile;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
       [Intent.ActionView],
       Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
       DataScheme = Utils.Constants.CallbackScheme,
       DataHost = Utils.Constants.CallbackHost)]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{

}
