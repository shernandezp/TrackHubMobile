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
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using TrackHubMobile.Messages;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Helpers;

public sealed class GraphQLReader(
    IHttpClientFactory httpClientFactory, 
    IAuthentication authentication,
    IStorage storage,
    ILogger<GraphQLReader> logger) : IGraphQLReader
{
    private readonly HttpClient client = httpClientFactory.CreateClient("GraphQL");
    private static readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> ExecuteGraphQLQuery<T>(
        string url,
        string query,
        string rootFieldName,
        CancellationToken cancellationToken)
    {
        var requestBody = new { query };
        var token = await GetTokenAsync();
        if (token is null)
        {
            return default;
        }

        using var response = await SendWithReauthenticationAsync(
            url, JsonSerializer.Serialize(requestBody), token, cancellationToken);
        if (response is null)
        {
            return default;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array)
        {
            // Without this line a server refusal — a disabled feature, a suspended account — is
            // indistinguishable from an empty result when someone has to explain it later.
            logger.LogWarning("GraphQL query for {RootField} returned errors: {Errors}",
                rootFieldName, errorsElement.GetRawText());
            return default;
        }

        if (root.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty(rootFieldName, out var fieldElement))
        {
            return JsonSerializer.Deserialize<T>(
                fieldElement.GetRawText(),
                _defaultJsonOptions);
        }

        return default;
    }

    /// <summary>
    /// Executes a GraphQL query and returns the data together with the first
    /// GraphQL error code/message (if any), so callers can react to specific
    /// server errors such as FEATURE_DISABLED instead of receiving default.
    /// </summary>
    public async Task<GraphQLResult<T>> ExecuteGraphQLQueryWithErrors<T>(
        string url,
        string query,
        string rootFieldName,
        CancellationToken cancellationToken)
    {
        var requestBody = new { query };
        var token = await GetTokenAsync();
        if (token is null)
        {
            // Not signed in yet: report it instead of sending an anonymous request
            return new GraphQLResult<T>(default, GraphQLResult<T>.UnauthenticatedCode, null);
        }

        using var response = await SendWithReauthenticationAsync(
            url, JsonSerializer.Serialize(requestBody), token, cancellationToken);
        if (response is null)
        {
            return new GraphQLResult<T>(default, GraphQLResult<T>.UnauthenticatedCode, null);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var root = doc.RootElement;

        string? errorCode = null;
        string? errorMessage = null;

        if (root.TryGetProperty("errors", out var errorsElement) &&
            errorsElement.ValueKind == JsonValueKind.Array &&
            errorsElement.GetArrayLength() > 0)
        {
            var firstError = errorsElement[0];

            if (firstError.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                errorMessage = messageElement.GetString();
            }

            if (firstError.TryGetProperty("extensions", out var extensionsElement) &&
                extensionsElement.ValueKind == JsonValueKind.Object &&
                extensionsElement.TryGetProperty("code", out var codeElement) &&
                codeElement.ValueKind == JsonValueKind.String)
            {
                errorCode = codeElement.GetString();
            }

            errorMessage ??= "GraphQL error";
        }

        T? data = default;
        if (root.TryGetProperty("data", out var dataElement) &&
            dataElement.ValueKind == JsonValueKind.Object &&
            dataElement.TryGetProperty(rootFieldName, out var fieldElement) &&
            fieldElement.ValueKind != JsonValueKind.Null)
        {
            data = JsonSerializer.Deserialize<T>(
                fieldElement.GetRawText(),
                _defaultJsonOptions);
        }

        return new GraphQLResult<T>(data, errorCode, errorMessage);
    }

    /// <summary>
    /// Sends the request and, if the provider rejects the token with 401, re-authenticates once and
    /// replays it. An unexpired token can still be revoked server-side, and without this the app
    /// shows an empty or stale fleet until it is restarted.
    /// <para>
    /// The body travels as a string, not as an <see cref="HttpContent"/>: disposing a request
    /// disposes its content, so a replay of the same object would throw instead of recovering.
    /// </para>
    /// </summary>
    private async Task<HttpResponseMessage?> SendWithReauthenticationAsync(
        string url,
        string requestJson,
        string token,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, requestJson, token, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        storage.ClearSecure(Constants.AccessToken);
        var refreshed = await authentication.RefreshAccessTokenAsync();
        if (string.IsNullOrEmpty(refreshed))
        {
            WeakReferenceMessenger.Default.Send(new SignInRequiredMessage());
            return null;
        }

        response = await SendAsync(url, requestJson, refreshed, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        WeakReferenceMessenger.Default.Send(new SignInRequiredMessage());
        return null;
    }

    private async Task<HttpResponseMessage> SendAsync(
        string url,
        string requestJson,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Returns a usable access token, attempting a silent refresh first.
    /// Null means an interactive sign-in is pending, so the query must not be sent.
    /// </summary>
    private async Task<string?> GetTokenAsync()
    {
        var token = await storage.GetSecure(Constants.AccessToken);
        if (TokenHelper.IsTokenValid(token))
        {
            return token;
        }

        token = await authentication.RefreshAccessTokenAsync();
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
