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

using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Services;

/// <summary>
/// The Manager class provides methods to interact with the Manager GraphQL API
/// for retrieving account settings and account feature flags.
/// </summary>
public sealed class Manager(IGraphQLReader reader) : IManager
{
    // Account settings are fetched once per session and cached in memory
    private AccountSettingsVm? _cachedSettings;

    /// <summary>
    /// Retrieves the account settings of the current user's account.
    /// The first successful result is cached for the session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>The <see cref="AccountSettingsVm"/> or null when unavailable.</returns>
    public async Task<AccountSettingsVm?> GetAccountSettingsAsync(CancellationToken cancellationToken)
    {
        if (_cachedSettings.HasValue)
        {
            return _cachedSettings;
        }

        // GraphQL query to fetch the account settings for the current user
        const string query = @"
        query {
          accountSettingsByUser {
            accountId
            maps
            mapsKey
            onlineInterval
            refreshMap
            refreshMapInterval
          }
        }";

        var response = await reader.ExecuteGraphQLQuery<AccountSettingsVm?>(Constants.ManagerUrl, query, "accountSettingsByUser", cancellationToken);
        if (response.HasValue && response.Value.AccountId != Guid.Empty)
        {
            _cachedSettings = response;
        }
        return response;
    }

    /// <summary>
    /// Retrieves the feature flags of an account.
    /// </summary>
    /// <param name="accountId">The unique identifier of the account.</param>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>A collection of <see cref="AccountFeatureVm"/> objects.</returns>
    public async Task<IEnumerable<AccountFeatureVm>> GetAccountFeaturesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        // GraphQL query to fetch the feature flags of the account
        string query = $@"
        query {{
          accountFeatures(query: {{ accountId: ""{accountId}"" }}) {{
            featureKey
            enabled
          }}
        }}";

        var response = await reader.ExecuteGraphQLQuery<IEnumerable<AccountFeatureVm>>(Constants.ManagerUrl, query, "accountFeatures", cancellationToken);
        return response ?? [];
    }

    /// <summary>
    /// Retrieves the current account's lifecycle status via the consolidated accountContext read.
    /// The read is allowed on non-operational accounts, so a suspended account still reports its status.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
    /// <returns>The status id, or null when unavailable.</returns>
    public async Task<short?> GetAccountStatusAsync(CancellationToken cancellationToken)
    {
        const string query = @"
        query {
          accountContext {
            statusId
          }
        }";

        var response = await reader.ExecuteGraphQLQuery<AccountContextVm?>(Constants.ManagerUrl, query, "accountContext", cancellationToken);
        return response?.StatusId;
    }
}
