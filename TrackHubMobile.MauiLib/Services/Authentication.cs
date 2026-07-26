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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Services;

public class Authentication(IHttpClientFactory httpClientFactory, IStorage storage) : IAuthentication
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("Auth");
    /// <summary>
    /// Initiates the login process by generating a code verifier and challenge, 
    /// constructing the authentication URL, and handling the authentication response.
    /// </summary>
    public async Task LoginAsync()
    {
        var codeVerifier = await storage.GetSecure(Constants.CodeVerifier);
        if (string.IsNullOrEmpty(codeVerifier))
        {
            codeVerifier = GenerateCodeVerifier();
        }

        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        var authUrl = $"{Constants.AuthUrl}?" +
            $"client_id={Constants.Client}" +
            $"&redirect_uri={HttpUtility.UrlEncode(Constants.CallbackUrl)}" +
            $"&response_type=code" +
            $"&scope={Constants.Scope} offline_access" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&state={state}";

        var callbackUrl = new Uri(Constants.CallbackUrl);
        var result = await WebAuthenticator.Default.AuthenticateAsync(new Uri(authUrl), callbackUrl);

        if (result.Properties.TryGetValue("code", out var authCode))
        {
            await ExchangeCodeForTokensAsync(authCode, codeVerifier);
        }
    }

    /// <summary>
    /// Logs the user out by revoking access and refresh tokens, clearing stored tokens, 
    /// and redirecting to the logout URL.
    /// </summary>
    public async Task LogoutAsync()
    {
        var accessToken = await storage.GetSecure(Constants.AccessToken);
        var refreshToken = await storage.GetSecure(Constants.RefreshToken);
        await RevokeTokenAsync(accessToken);
        await RevokeTokenAsync(refreshToken);

        storage.ClearSecure(Constants.AccessToken);
        storage.ClearSecure(Constants.RefreshToken);

        var logoutUrl = $"{Constants.LogoutUrl}?post_logout_redirect_uri={HttpUtility.UrlEncode(Constants.LogoutCallbackUrl)}";
        await Browser.Default.OpenAsync(new Uri(logoutUrl), BrowserLaunchMode.External);
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

        var tokenResult = JsonSerializer.Deserialize<JsonElement>(content);
        var accessToken = tokenResult.GetProperty("access_token").GetString();
        var refreshToken = tokenResult.GetProperty("refresh_token").GetString();

        await storage.SetSecure(Constants.AccessToken, accessToken);
        await storage.SetSecure(Constants.RefreshToken, refreshToken);
    }

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
    /// Refreshes the access token using the stored refresh token and updates the stored tokens.
    /// </summary>
    public async Task<string?> RefreshAccessTokenAsync()
    {
        var refreshToken = await storage.GetSecure(Constants.RefreshToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            await LoginAsync();
            return null;
        }

        var tokenRequest = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", Constants.Client),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
        ]);

        var response = await httpClient.PostAsync(Constants.TokenUrl, tokenRequest);
        if (!response.IsSuccessStatusCode)
        {
            await LoginAsync();
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseContent);
        var newAccessToken = document.RootElement.GetProperty("access_token").GetString();
        var newRefreshToken = document.RootElement.GetProperty("refresh_token").GetString();

        // Store the new tokens and expiration time
        await storage.SetSecure(Constants.AccessToken, newAccessToken);
        await storage.SetSecure(Constants.RefreshToken, newRefreshToken);
        return newAccessToken;
    }

    /// <summary>
    /// Revokes a given token by sending a revoke request to the authentication server.
    /// </summary>
    /// <param name="token">The token to revoke.</param>
    public async Task RevokeTokenAsync(string? token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            var revokeRequest = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", Constants.Client),
                        new KeyValuePair<string, string>("token", token)
            ]);
            var response = await httpClient.PostAsync(Constants.RevokeUrl, revokeRequest);
            response.EnsureSuccessStatusCode();
        }
    }
}
