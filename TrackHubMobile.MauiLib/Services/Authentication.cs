// Copyright (c) 2025 Sergio Hernandez. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using TrackHubMobile.Helpers;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Services;

public class Authentication(
    IHttpClientFactory httpClientFactory,
    IStorage storage,
    ILocalizationResourceManager localization) : IAuthentication
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("Auth");

    // Only one token acquisition may run at a time. WebAuthenticator keeps a single
    // pending session per process: starting a second one cancels the first, and that
    // cancellation surfaces on whichever thread awaited it.
    private readonly SemaphoreSlim gate = new(1, 1);

    // Set when the user dismisses the browser, so coming back to the app does not
    // immediately reopen it. Signing out clears it.
    private static readonly TimeSpan DeclineCooldown = TimeSpan.FromMinutes(2);
    private DateTimeOffset? declinedAt;

    /// <summary>
    /// Ensures there is a usable access token: reuses the stored one, then tries a
    /// silent refresh, and only then opens the browser for an interactive sign-in.
    /// Never throws — a cancelled or failed sign-in returns false.
    /// </summary>
    public async Task<bool> LoginAsync()
    {
        if (await HasValidAccessTokenAsync())
        {
            return true;
        }

        await gate.WaitAsync();
        try
        {
            // Another caller may have completed the flow while we waited on the gate
            if (await HasValidAccessTokenAsync())
            {
                return true;
            }

            if (await TryRefreshAsync() is not null)
            {
                return true;
            }

            if (declinedAt.HasValue && DateTimeOffset.UtcNow - declinedAt.Value < DeclineCooldown)
            {
                return false;
            }

            return await AuthenticateInteractivelyAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// True when a usable access token is available, refreshing it silently if needed.
    /// Never prompts the user.
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
        => await HasValidAccessTokenAsync() || await RefreshAccessTokenAsync() is not null;

    /// <summary>
    /// Logs the user out by revoking access and refresh tokens, clearing stored tokens,
    /// and redirecting to the logout URL.
    /// </summary>
    public async Task LogoutAsync()
    {
        await gate.WaitAsync();
        try
        {
            var accessToken = await storage.GetSecure(Constants.AccessToken);
            var refreshToken = await storage.GetSecure(Constants.RefreshToken);

            storage.ClearSecure(Constants.AccessToken);
            storage.ClearSecure(Constants.RefreshToken);
            declinedAt = null;

            // Best effort: the local session is already gone even if the server call fails
            await RevokeTokenAsync(accessToken);
            await RevokeTokenAsync(refreshToken);
        }
        finally
        {
            gate.Release();
        }

        try
        {
            var logoutUrl = $"{Constants.LogoutUrl}?post_logout_redirect_uri={HttpUtility.UrlEncode(Constants.LogoutCallbackUrl)}";
            await Browser.Default.OpenAsync(new Uri(logoutUrl), BrowserLaunchMode.External);
        }
        catch
        {
            Notify(localization["Error"]);
        }
    }

    /// <summary>
    /// Refreshes the access token silently using the stored refresh token.
    /// Returns null when an interactive sign-in is required; it never opens the
    /// browser itself, because callers run on background threads (data refresh,
    /// GraphQL reads) where a second WebAuthenticator session would cancel a
    /// pending one. Re-prompting is driven by the app lifecycle instead.
    /// </summary>
    public async Task<string?> RefreshAccessTokenAsync()
    {
        await gate.WaitAsync();
        try
        {
            var token = await storage.GetSecure(Constants.AccessToken);
            if (TokenHelper.IsTokenValid(token))
            {
                return token;
            }

            return await TryRefreshAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Runs the browser-based authorization code flow with PKCE and stores the tokens.
    /// The gate is expected to be held by the caller.
    /// </summary>
    private async Task<bool> AuthenticateInteractivelyAsync()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        var authUrl = $"{Constants.AuthUrl}?" +
            $"client_id={Constants.Client}" +
            $"&redirect_uri={HttpUtility.UrlEncode(Constants.CallbackUrl)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString($"{Constants.Scope} offline_access")}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&state={state}";

        try
        {
            // WebAuthenticator must be started from the UI thread
            var result = await MainThread.InvokeOnMainThreadAsync(() =>
                WebAuthenticator.Default.AuthenticateAsync(new Uri(authUrl), new Uri(Constants.CallbackUrl)));

            if (result.Properties.TryGetValue("state", out var returnedState) &&
                !string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The authorization response state does not match the request.");
            }

            if (!result.Properties.TryGetValue("code", out var authCode) || string.IsNullOrEmpty(authCode))
            {
                throw new InvalidOperationException("The authorization response did not contain a code.");
            }

            await ExchangeCodeForTokensAsync(authCode, codeVerifier);
            declinedAt = null;
            return true;
        }
        catch (OperationCanceledException)
        {
            // The user dismissed the browser, or a competing session superseded this
            // one. Expected outcome, not an error.
            declinedAt = DateTimeOffset.UtcNow;
            return false;
        }
        catch
        {
            Notify(localization["SignInFailed"]);
            return false;
        }
    }

    /// <summary>
    /// Exchanges the authorization code for access and refresh tokens,
    /// and securely stores them.
    /// </summary>
    /// <param name="code">The authorization code received from the authentication server.</param>
    /// <param name="codeVerifier">The code verifier used to generate the code challenge.</param>
    private async Task ExchangeCodeForTokensAsync(string code, string codeVerifier)
    {
        var body = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", Constants.Client },
                    { "redirect_uri", Constants.CallbackUrl },
                    { "code", code },
                    { "code_verifier", codeVerifier }
                };

        var response = await httpClient.PostAsync(Constants.TokenUrl, new FormUrlEncodedContent(body));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Token error: {content}");
        }

        if (await StoreTokensAsync(content) is null)
        {
            throw new Exception("Token response did not contain an access token.");
        }
    }

    /// <summary>
    /// Silent refresh. Returns the new access token, or null when the stored refresh
    /// token is missing, rejected, or unreachable. The gate is expected to be held.
    /// </summary>
    private async Task<string?> TryRefreshAsync()
    {
        var refreshToken = await storage.GetSecure(Constants.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        try
        {
            var tokenRequest = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", Constants.Client),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            ]);

            var response = await httpClient.PostAsync(Constants.TokenUrl, tokenRequest);
            if (!response.IsSuccessStatusCode)
            {
                // A rejected grant is final; transient failures keep the token for a later retry
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    storage.ClearSecure(Constants.RefreshToken);
                }
                return null;
            }

            return await StoreTokensAsync(await response.Content.ReadAsStringAsync());
        }
        catch
        {
            // Offline, timed out or malformed response — an interactive sign-in decides
            return null;
        }
    }

    /// <summary>
    /// Stores the tokens from a token endpoint response and returns the access token,
    /// or null when the response carries none.
    /// </summary>
    private async Task<string?> StoreTokensAsync(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var accessTokenElement)
            ? accessTokenElement.GetString()
            : null;

        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        await storage.SetSecure(Constants.AccessToken, accessToken);

        // Rotated refresh tokens replace the stored one; a response without one keeps it
        if (root.TryGetProperty("refresh_token", out var refreshTokenElement) &&
            refreshTokenElement.GetString() is { Length: > 0 } refreshToken)
        {
            await storage.SetSecure(Constants.RefreshToken, refreshToken);
        }

        return accessToken;
    }

    private async Task<bool> HasValidAccessTokenAsync()
        => TokenHelper.IsTokenValid(await storage.GetSecure(Constants.AccessToken));

    private static void Notify(string message)
        => MainThread.BeginInvokeOnMainThread(() =>
            WeakReferenceMessenger.Default.Send(new ToastMessage(message, true)));

    /// <summary>
    /// Generates a secure code verifier for PKCE (Proof Key for Code Exchange).
    /// </summary>
    /// <returns>A base64 URL-encoded string representing the code verifier.</returns>
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Generates a code challenge from the given code verifier using SHA256 hashing.
    /// </summary>
    /// <param name="codeVerifier">The code verifier to hash.</param>
    /// <returns>A base64 URL-encoded string representing the code challenge.</returns>
    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Revokes a given token by sending a revoke request to the authentication server.
    /// </summary>
    /// <param name="token">The token to revoke.</param>
    private async Task RevokeTokenAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        try
        {
            var revokeRequest = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", Constants.Client),
                new KeyValuePair<string, string>("token", token)
            ]);
            await httpClient.PostAsync(Constants.RevokeUrl, revokeRequest);
        }
        catch
        {
            // The token is dropped locally regardless; nothing to recover here
        }
    }
}
