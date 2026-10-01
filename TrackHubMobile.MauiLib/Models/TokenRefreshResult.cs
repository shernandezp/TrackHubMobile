// Copyright (c) 2026 Sergio Hernandez. All rights reserved.
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

// Neither a token nor SignInRequired means the token endpoint could not be reached: the stored
// refresh token is still good and the caller should retry later.
public readonly record struct TokenRefreshResult(string? AccessToken, bool SignInRequired)
{
    public static TokenRefreshResult Refreshed(string accessToken) => new(accessToken, false);

    public static TokenRefreshResult Rejected { get; } = new(null, true);

    public static TokenRefreshResult Unavailable { get; } = new(null, false);

    public bool Succeeded => !string.IsNullOrEmpty(AccessToken);
}
