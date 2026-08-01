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

namespace TrackHubMobile.Models;

/// <summary>
/// Wraps a GraphQL response so callers can distinguish between
/// an empty result and a server-side error (e.g. FEATURE_DISABLED).
/// </summary>
public readonly record struct GraphQLResult<T>(
    T? Data,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>
    /// Error code used when the query was never sent because no access token was available.
    /// </summary>
    public const string UnauthenticatedCode = "UNAUTHENTICATED";

    public bool HasError => ErrorCode is not null || ErrorMessage is not null;

    public bool IsUnauthenticated =>
        string.Equals(ErrorCode, UnauthenticatedCode, StringComparison.Ordinal);

    public bool IsFeatureDisabled =>
        (ErrorCode?.Contains("FEATURE_DISABLED", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (ErrorMessage?.Contains("FEATURE_DISABLED", StringComparison.OrdinalIgnoreCase) ?? false) ||
        // FeatureDisabledException message shape: "Feature '<key>' is not enabled..."
        (ErrorMessage?.Contains("is not enabled", StringComparison.OrdinalIgnoreCase) ?? false);
}
