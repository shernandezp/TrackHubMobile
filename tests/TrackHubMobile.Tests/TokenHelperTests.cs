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
using TrackHubMobile.Helpers;

namespace TrackHubMobile.Tests;

/// <summary>
/// When the app hands out the token it holds. A token the device clock still calls valid but the
/// server has already expired produces a failed screen the app cannot recover from on its own, so
/// the margin is the whole point of this check.
/// </summary>
[TestFixture]
public class TokenHelperTests
{
    [Test]
    public void Token_WithTimeLeft_IsValid()
        => Assert.That(TokenHelper.IsTokenValid(TokenExpiringIn(TimeSpan.FromMinutes(10))), Is.True);

    [Test]
    public void Token_InsideTheExpiryMargin_IsNotValid()
        => Assert.That(TokenHelper.IsTokenValid(TokenExpiringIn(TimeSpan.FromSeconds(30))), Is.False);

    [Test]
    public void Token_AlreadyExpired_IsNotValid()
        => Assert.That(TokenHelper.IsTokenValid(TokenExpiringIn(TimeSpan.FromMinutes(-1))), Is.False);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-token")]
    [TestCase("only.two")]
    public void Token_ThatIsNotAJwt_IsNotValid(string? token)
        => Assert.That(TokenHelper.IsTokenValid(token), Is.False);

    [Test]
    public void Token_WithNoExpiryClaim_IsNotValid()
        => Assert.That(TokenHelper.IsTokenValid(Jwt("{}")), Is.False);

    private static string TokenExpiringIn(TimeSpan remaining)
        => Jwt(JsonSerializer.Serialize(new { exp = DateTimeOffset.UtcNow.Add(remaining).ToUnixTimeSeconds() }));

    private static string Jwt(string payloadJson)
        => $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(payloadJson)}.signature";

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
