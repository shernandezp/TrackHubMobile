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

namespace TrackHubMobile.Utils;

public static class Constants
{
    public const string AuthUrl = "https://track-hub.co/Identity/authorize";
    public const string TokenUrl = "https://track-hub.co/Identity/token";
    public const string RevokeUrl = "https://track-hub.co/Identity/revoke";
    public const string LogoutUrl = "https://track-hub.co/Identity/logout";

    public const string CallbackUrl = "trackhubmobile://callback";
    public const string CallbackScheme = "trackhubmobile";
    public const string CallbackHost = "callback";
    public const string LogoutCallbackUrl = "trackhubmobile://logoutcallback";
    public const string LogoutScheme = "trackhubmobile";
    public const string LogoutHost = "logoutcallback";
    public const string Client = "mobile_client";
    public const string Scope = "mobile_scope";

    public const string CodeVerifier = "code_verifier";
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
}
