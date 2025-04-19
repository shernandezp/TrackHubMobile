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
        var token = await storage.GetSecure(Constants.AccessToken);
        if (TokenHelper.IsTokenValid(token)) 
        {
            token = await authentication.RefreshAccessTokenAsync();
        }

        using var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsync(url, jsonContent, cancellationToken);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array)
        {
            // Notify UI
            // Log errorsElement.GetRawText()
            var errors = errorsElement.EnumerateArray()
                .Select(e => e.GetProperty("message").GetString())
                .Where(msg => !string.IsNullOrWhiteSpace(msg))
                .ToList()!;

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
}
