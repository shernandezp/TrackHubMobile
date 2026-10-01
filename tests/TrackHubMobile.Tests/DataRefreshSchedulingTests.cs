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

using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;
using TrackHubMobile.Services;

namespace TrackHubMobile.Tests;

[TestFixture]
public class DataRefreshSchedulingTests
{
    private static readonly GraphQLResult<IEnumerable<PositionVm>> SignedOut =
        new(null, GraphQLResult<IEnumerable<PositionVm>>.UnauthenticatedCode, null);

    private sealed class ManualClock : TimeProvider
    {
        private readonly object gate = new();
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate) return now;
        }

        public void Advance(TimeSpan by)
        {
            lock (gate) now += by;
        }
    }

    private sealed class SettingsManager : IManager
    {
        private int settingsCalls;

        public int SettingsCalls => Volatile.Read(ref settingsCalls);

        public AccountSettingsVm? Answer { get; set; }

        public Task<AccountSettingsVm?> GetAccountSettingsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref settingsCalls);
            return Task.FromResult(Answer);
        }

        public Task<IEnumerable<AccountFeatureVm>> GetAccountFeaturesAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<AccountFeatureVm>>([]);
        public Task<short?> GetAccountStatusAsync(CancellationToken cancellationToken) => Task.FromResult<short?>(2);
        public Task<TimeZoneInfo> GetAccountTimeZoneAsync(CancellationToken cancellationToken) => Task.FromResult(TimeZoneInfo.Utc);
        public void ResetSession() { }
    }

    // The first read ignores cancellation, like a response already on the wire when the screen changes.
    private sealed class Router : IRouter
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public bool HoldFirst { get; init; }

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<GraphQLResult<IEnumerable<PositionVm>>> FirstResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LaterStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<GraphQLResult<IEnumerable<PositionVm>>> GetDevicePositionsByUserAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1 && HoldFirst)
            {
                FirstStarted.TrySetResult();
                return FirstResponse.Task;
            }

            LaterStarted.TrySetResult();
            return Task.FromResult(SignedOut);
        }

        public Task<PositionVm> GetDeviceAsync(Guid transporterId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GraphQLResult<IEnumerable<TripVm>>> GetTripsByTransporterAsync(Guid transporterId, DateTimeOffset from, DateTimeOffset to, string? source, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<GraphQLResult<IEnumerable<PositionVm>>> GetPositionsByTransporterAsync(Guid transporterId, DateTimeOffset from, DateTimeOffset to, string? source, CancellationToken cancellationToken)
            => throw new NotSupportedException();
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
    public async Task AFailingSettingsRead_BacksOffExponentially_AndStopsOnceApplied()
    {
        var clock = new ManualClock();
        var manager = new SettingsManager();
        var refresh = new DataRefresh(new Router(), manager, new SignedIn(), new NoText(), clock);

        try
        {
            refresh.SetScreenActive(true);
            await WaitUntilAsync(() => manager.SettingsCalls == 1);

            await refresh.ForceRefreshAsync();
            Assert.That(manager.SettingsCalls, Is.EqualTo(1), "the next read waits one refresh interval");

            clock.Advance(TimeSpan.FromSeconds(30));
            await refresh.ForceRefreshAsync();
            Assert.That(manager.SettingsCalls, Is.EqualTo(2));

            clock.Advance(TimeSpan.FromSeconds(30));
            await refresh.ForceRefreshAsync();
            Assert.That(manager.SettingsCalls, Is.EqualTo(2), "the second failure doubles the delay");

            clock.Advance(TimeSpan.FromSeconds(30));
            await refresh.ForceRefreshAsync();
            Assert.That(manager.SettingsCalls, Is.EqualTo(3));

            manager.Answer = new AccountSettingsVm(Guid.NewGuid(), "OpenStreetMap", "key", 30, true, 10);
            clock.Advance(TimeSpan.FromMinutes(2));
            await refresh.ForceRefreshAsync();
            clock.Advance(TimeSpan.FromMinutes(10));
            await refresh.ForceRefreshAsync();

            Assert.That(manager.SettingsCalls, Is.EqualTo(4), "applied settings are read once per session");
        }
        finally
        {
            await refresh.DisposeAsync();
        }
    }

    [Test]
    public async Task ATickSkippedWhileThePreviousOneFinishes_RunsAsSoonAsItEnds()
    {
        var router = new Router { HoldFirst = true };
        var refresh = new DataRefresh(router, new SettingsManager(), new SignedIn(), new NoText());

        try
        {
            refresh.SetScreenActive(true);
            await router.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            refresh.SetScreenActive(false);
            refresh.SetScreenActive(true);
            await Task.Delay(200);
            Assert.That(router.Calls, Is.EqualTo(1), "the new screen's first tick found the gate taken");

            router.FirstResponse.SetResult(SignedOut);

            Assert.That(async () => await router.LaterStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)), Throws.Nothing,
                "the skipped tick runs on release instead of a full period later");
        }
        finally
        {
            await refresh.DisposeAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
