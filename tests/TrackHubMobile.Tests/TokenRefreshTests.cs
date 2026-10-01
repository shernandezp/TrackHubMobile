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

using System.Net;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackHubMobile.Helpers;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;
using TrackHubMobile.Services;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Tests;

[TestFixture]
public class TokenRefreshTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class MemoryStorage : IStorage
    {
        public Dictionary<string, string?> Values { get; } = [];

        public void ClearSecure(string key) => Values.Remove(key);

        public Task<string?> GetSecure(string key) => Task.FromResult(Values.GetValueOrDefault(key));

        public Task SetSecure(string key, string? value)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedRefresh(TokenRefreshResult result) : IAuthentication
    {
        public Task<bool> LoginAsync() => Task.FromResult(false);
        public Task<string?> SignInAsync(string email, string password) => Task.FromResult<string?>(null);
        public Task LogoutAsync() => Task.CompletedTask;
        public Task<bool> HasSessionAsync() => Task.FromResult(true);
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(result.Succeeded);
        public Task<TokenRefreshResult> RefreshAccessTokenAsync() => Task.FromResult(result);
    }

    private sealed class NoText : ILocalizationResourceManager
    {
        public string this[string key] => key;
    }

    private readonly object recipient = new();
    private int signInPrompts;

    [SetUp]
    public void ListenForSignInPrompts()
    {
        signInPrompts = 0;
        WeakReferenceMessenger.Default.Register<SignInRequiredMessage>(recipient, (_, _) => signInPrompts++);
    }

    [TearDown]
    public void StopListening() => WeakReferenceMessenger.Default.Unregister<SignInRequiredMessage>(recipient);

    [Test]
    public void ABlipWhileRefreshingAfterA401_IsARetryableError_NotASignInPrompt()
    {
        var reader = ReaderAnswering401(TokenRefreshResult.Unavailable, out var storage);

        Assert.ThrowsAsync<HttpRequestException>(() =>
            reader.ExecuteGraphQLQueryWithErrors<IEnumerable<PositionVm>>(Constants.RouterUrl, "query { x }", "x", CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(signInPrompts, Is.Zero);
            Assert.That(storage.Values[Constants.RefreshToken], Is.EqualTo("refresh"));
        });
    }

    [Test]
    public async Task ARejectedRefreshAfterA401_AsksForSignIn()
    {
        var reader = ReaderAnswering401(TokenRefreshResult.Rejected, out _);

        var result = await reader.ExecuteGraphQLQueryWithErrors<IEnumerable<PositionVm>>(
            Constants.RouterUrl, "query { x }", "x", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsUnauthenticated, Is.True);
            Assert.That(signInPrompts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnUnreachableTokenEndpoint_KeepsTheRefreshToken()
    {
        var storage = new MemoryStorage();
        storage.Values[Constants.RefreshToken] = "refresh";
        var authentication = new Authentication(
            new Factory(new Handler(_ => throw new HttpRequestException("offline"))), storage, new NoText());

        var result = await authentication.RefreshAccessTokenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.SignInRequired, Is.False);
            Assert.That(storage.Values[Constants.RefreshToken], Is.EqualTo("refresh"));
        });
    }

    [Test]
    public async Task AServerErrorFromTheTokenEndpoint_KeepsTheRefreshToken()
    {
        var storage = new MemoryStorage();
        storage.Values[Constants.RefreshToken] = "refresh";
        var authentication = new Authentication(
            new Factory(new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))), storage, new NoText());

        var result = await authentication.RefreshAccessTokenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.SignInRequired, Is.False);
            Assert.That(storage.Values[Constants.RefreshToken], Is.EqualTo("refresh"));
        });
    }

    private static GraphQLReader ReaderAnswering401(TokenRefreshResult refresh, out MemoryStorage storage)
    {
        storage = new MemoryStorage();
        storage.Values[Constants.AccessToken] = ValidJwt();
        storage.Values[Constants.RefreshToken] = "refresh";
        return new GraphQLReader(
            new Factory(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))),
            new FixedRefresh(refresh),
            storage,
            NullLogger<GraphQLReader>.Instance);
    }

    private static string ValidJwt()
    {
        var payload = JsonSerializer.Serialize(new { exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() });
        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(payload)}.signature";
    }

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
