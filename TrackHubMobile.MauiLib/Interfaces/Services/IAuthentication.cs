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

namespace TrackHubMobile.Interfaces.Services;

public interface IAuthentication
{
    /// <summary>
    /// Ensures a usable access token silently (stored token, then refresh). Returns false when the
    /// user has to sign in; it never throws and never prompts.
    /// </summary>
    Task<bool> LoginAsync();

    /// <summary>
    /// Signs in with the password grant and stores the tokens. Returns null on success, otherwise
    /// a localized message for the sign-in page.
    /// </summary>
    Task<string?> SignInAsync(string email, string password);

    Task LogoutAsync();

    /// <summary>Stored tokens exist (valid or refreshable). No network call.</summary>
    Task<bool> HasSessionAsync();

    /// <summary>
    /// True when a usable access token is available, refreshing it silently if needed.
    /// Never prompts the user.
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Silently refreshes the access token. Returns null when an interactive sign-in is required.
    /// </summary>
    Task<string?> RefreshAccessTokenAsync();
}
