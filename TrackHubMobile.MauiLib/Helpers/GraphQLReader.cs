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

using System.Text;
using System.Text.Json;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Helpers;

public sealed class GraphQLReader(
    IHttpClientFactory httpClientFactory, 
    IAuthentication authentication,
    IStorage storage) : IGraphQLReader
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

        using var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = jsonContent
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array)
        {
            var errors = errorsElement.EnumerateArray()
                .Select(e => e.GetProperty("message").GetString())
                .Where(msg => !string.IsNullOrWhiteSpace(msg))
                .ToList()!;
            // TODO: Log errorsElement.GetRawText()

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

        using var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = jsonContent
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
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
