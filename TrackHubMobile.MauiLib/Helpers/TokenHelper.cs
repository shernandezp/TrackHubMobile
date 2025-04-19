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

using System.Text.Json;

namespace TrackHubMobile.Helpers;

public class TokenHelper
{
    public static bool IsTokenValid(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }
        try
        {
            // Split the token into its parts (header, payload, signature)
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false; // Invalid token format
            }

            // Decode the payload (second part of the token)
            var payloadJson = Base64UrlDecode(parts[1]);
            var payload = JsonSerializer.Deserialize<JwtPayload>(payloadJson);

            if (payload == null || payload.Exp == null)
            {
                return false; // Missing expiration claim
            }

            // Convert expiration time to DateTime
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(payload.Exp.Value).UtcDateTime;
            var currentTime = DateTime.UtcNow;

            // Check if the token has expired
            return expirationTime > currentTime;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        var base64Bytes = Convert.FromBase64String(output);
        return System.Text.Encoding.UTF8.GetString(base64Bytes);
    }

    private class JwtPayload
    {
        public long? Exp { get; set; }
    }
}
