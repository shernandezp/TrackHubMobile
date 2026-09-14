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
using System.Text.Json;
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

    // Only one token acquisition at a time, so concurrent callers never race two refreshes
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<bool> LoginAsync()
    {
        if (await IsAuthenticatedAsync())
        {
            return true;
        }

        if (!await HasSessionAsync())
        {
            RequireSignIn();
        }

        return false;
    }

    public async Task<string?> SignInAsync(string email, string password)
    {
        await gate.WaitAsync();
        try
        {
            var tokenRequest = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", Constants.Client),
                new KeyValuePair<string, string>("username", email),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("scope", Constants.Scope + " offline_access")
            ]);

            var response = await httpClient.PostAsync(Constants.TokenUrl, tokenRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return DescribeTokenError(content);
            }

            return await StoreTokensAsync(content) is null ? localization["SignInFailed"] : null;
        }
        catch
        {
            return localization["SignInFailed"];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> HasSessionAsync()
        => TokenHelper.IsTokenValid(await storage.GetSecure(Constants.AccessToken))
            || !string.IsNullOrEmpty(await storage.GetSecure(Constants.RefreshToken));

    public async Task<bool> IsAuthenticatedAsync()
        => await HasValidAccessTokenAsync() || await RefreshAccessTokenAsync() is not null;

    public async Task LogoutAsync()
    {
        await gate.WaitAsync();
        try
        {
            var accessToken = await storage.GetSecure(Constants.AccessToken);
            var refreshToken = await storage.GetSecure(Constants.RefreshToken);

            storage.ClearSecure(Constants.AccessToken);
            storage.ClearSecure(Constants.RefreshToken);

            // Best effort: the local session is already gone even if the server call fails
            await RevokeTokenAsync(accessToken);
            await RevokeTokenAsync(refreshToken);
        }
        finally
        {
            gate.Release();
        }

        RequireSignIn();
    }

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
                    RequireSignIn();
                }
                return null;
            }

            return await StoreTokensAsync(await response.Content.ReadAsStringAsync());
        }
        catch
        {
            // Offline, timed out or malformed response; the next tick retries
            return null;
        }
    }

    private string DescribeTokenError(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var error = root.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : null;
            var description = root.TryGetProperty("error_description", out var descriptionElement)
                ? descriptionElement.GetString() ?? string.Empty
                : string.Empty;

            if (error == "invalid_grant")
            {
                if (description.Contains("locked", StringComparison.OrdinalIgnoreCase))
                {
                    return localization["AccountLocked"];
                }

                return description.Contains("incorrect", StringComparison.OrdinalIgnoreCase)
                    || description.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                    ? localization["InvalidCredentials"]
                    : localization["AccountUnavailable"];
            }
        }
        catch (JsonException)
        {
        }

        return localization["SignInFailed"];
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

    private static void RequireSignIn()
        => MainThread.BeginInvokeOnMainThread(() =>
            WeakReferenceMessenger.Default.Send(new SignInRequiredMessage()));

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
