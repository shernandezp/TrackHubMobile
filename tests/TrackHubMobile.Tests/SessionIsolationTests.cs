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

using CommunityToolkit.Mvvm.Messaging;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;
using TrackHubMobile.Services;

namespace TrackHubMobile.Tests;

[TestFixture]
public class SessionIsolationTests
{
    private sealed class GatedReader : IGraphQLReader
    {
        public int Calls { get; private set; }

        public TaskCompletionSource<AccountSettingsVm?> Settings { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<T?> ExecuteGraphQLQuery<T>(string url, string query, string rootFieldName, CancellationToken cancellationToken)
        {
            Calls++;
            object? answer = await Settings.Task;
            return (T?)answer;
        }

        public Task<GraphQLResult<T>> ExecuteGraphQLQueryWithErrors<T>(string url, string query, string rootFieldName, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    // The first read ignores cancellation, like a response already on the wire when the user signs out.
    private sealed class SlowRouter : IRouter
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<GraphQLResult<IEnumerable<PositionVm>>> FirstResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LaterStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GraphQLResult<IEnumerable<PositionVm>>> GetDevicePositionsByUserAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                FirstStarted.TrySetResult();
                return await FirstResponse.Task;
            }

            LaterStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default;
        }

        public Task<PositionVm> GetDeviceAsync(Guid transporterId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphQLResult<IEnumerable<TripVm>>> GetTripsByTransporterAsync(Guid transporterId, DateTimeOffset from, DateTimeOffset to, string? source, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<GraphQLResult<IEnumerable<PositionVm>>> GetPositionsByTransporterAsync(Guid transporterId, DateTimeOffset from, DateTimeOffset to, string? source, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class ActiveAccount : IManager
    {
        public Task<AccountSettingsVm?> GetAccountSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<AccountSettingsVm?>(null);
        public Task<IEnumerable<AccountFeatureVm>> GetAccountFeaturesAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<AccountFeatureVm>>([]);
        public Task<short?> GetAccountStatusAsync(CancellationToken cancellationToken) => Task.FromResult<short?>(2);
        public Task<TimeZoneInfo> GetAccountTimeZoneAsync(CancellationToken cancellationToken) => Task.FromResult(TimeZoneInfo.Utc);
        public void ResetSession() { }
    }

    private sealed class SignedIn : IAuthentication
    {
        public Task<bool> LoginAsync() => Task.FromResult(true);
        public Task<string?> SignInAsync(string email, string password) => Task.FromResult<string?>(null);
        public Task LogoutAsync() => Task.CompletedTask;
        public Task<bool> HasSessionAsync() => Task.FromResult(true);
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
        public Task<TokenRefreshResult> RefreshAccessTokenAsync() => Task.FromResult(TokenRefreshResult.Refreshed("token"));
    }

    private sealed class NoText : ILocalizationResourceManager
    {
        public string this[string key] => key;
    }

    [Test]
    public async Task ASettingsReadStartedBeforeSignOut_DoesNotSeedTheNextSession()
    {
        var reader = new GatedReader();
        var manager = new Manager(reader);
        var first = new AccountSettingsVm(Guid.NewGuid(), "OpenStreetMap", "key-a", 30, true, 10);

        var inFlight = manager.GetAccountSettingsAsync(CancellationToken.None);
        manager.ResetSession();
        reader.Settings.SetResult(first);
        await inFlight;

        var second = new AccountSettingsVm(Guid.NewGuid(), "OpenStreetMap", "key-b", 60, true, 20);
        reader.Settings = new TaskCompletionSource<AccountSettingsVm?>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Settings.SetResult(second);
        var next = await manager.GetAccountSettingsAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(reader.Calls, Is.EqualTo(2));
            Assert.That(next!.Value.MapsKey, Is.EqualTo("key-b"));
        });
    }

    [Test]
    public async Task ARefreshStartedBeforeSignOut_NeverPublishes_AndHoldsOffTheNextOneUntilItEnds()
    {
        var router = new SlowRouter();
        var refresh = new DataRefresh(router, new ActiveAccount(), new SignedIn(), new NoText());
        var recipient = new object();
        var published = new List<int>();
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(recipient, (_, m) =>
        {
            lock (published) published.Add(m.Value.Count());
        });

        try
        {
            refresh.SetScreenActive(true);
            await router.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            refresh.ResetSession();
            var forced = refresh.ForceRefreshAsync();
            await Task.Delay(200);
            Assert.That(router.Calls, Is.EqualTo(1), "a forced refresh waits for the one in flight");

            router.FirstResponse.SetResult(new GraphQLResult<IEnumerable<PositionVm>>([Unit()], null, null));
            await router.LaterStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() =>
            {
                Assert.That(refresh.Transporters, Is.Empty);
                lock (published) Assert.That(published, Is.All.EqualTo(0));
            });

            await refresh.DisposeAsync();
            await forced.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<DataRefreshedMessage>(recipient);
            await refresh.DisposeAsync();
        }
    }

    private static PositionVm Unit()
        => new(Guid.NewGuid(), "unit", "TRUCK", 0, 0, null, DateTimeOffset.UtcNow, 40,
            null, null, null, null, null, null, null);
}
