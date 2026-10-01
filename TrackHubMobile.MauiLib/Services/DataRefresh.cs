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
using TrackHubMobile.Messages;
using TrackHubMobile.Models;

namespace TrackHubMobile.Services;

public class DataRefresh(
    IRouter router,
    IManager manager,
    IAuthentication authentication,
    ILocalizationResourceManager localization,
    TimeProvider? timeProvider = null) : IAsyncDisposable, IDataRefresh
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxSettingsRetryDelay = TimeSpan.FromMinutes(5);
    // Absorbs timer jitter so a retry due one interval later is not pushed back a whole extra tick.
    private static readonly TimeSpan SettingsRetryTolerance = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private ITimer? _timer;
    private bool _isActiveScreen;
    private bool _isAppActive = true;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _timerLock = new();

    // Held for the whole refresh: a tick still unwinding after a screen change keeps the next one out
    // instead of racing it.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    // A tick that finds the gate taken leaves this set; whoever releases the gate runs it as a catch-up.
    private int _tickPending;

    // Sign-out cancels the session, and every write a refresh makes is checked against it under this
    // lock, so a refresh that started before sign-out can never publish into the next session.
    private readonly object _sessionLock = new();
    private CancellationTokenSource _session = new();

    // Account-settings-driven refresh configuration (defaults: enabled, 30 s)
    private TimeSpan _refreshInterval = DefaultRefreshInterval;
    private bool _refreshEnabled = true;
    private int _settingsFetchStarted;
    private TimeSpan _settingsRetryDelay;
    private DateTimeOffset _settingsRetryAt = DateTimeOffset.MinValue;

    // Account operational-status gating: once a non-operational status is observed,
    // operational queries are suppressed and a suspension message is raised.
    private int _accountStatusFetchStarted;
    private volatile bool _accountOperational = true;

    public IEnumerable<PositionVm> Transporters { get; private set; } = [];

    public UnitStatusRules StatusRules { get; private set; } = UnitStatusRules.Default;

    public void SetScreenActive(bool isActive)
    {
        _isActiveScreen = isActive;
        CheckTimerStatus();
    }

    public async Task SetAppActive(bool isActive, bool forceRefresh = false)
    {
        _isAppActive = isActive;
        CheckTimerStatus();
        if (forceRefresh && _isActiveScreen)
        {
            await ForceRefreshAsync();
        }
    }

    // Waits for a refresh already in flight instead of running beside it.
    public async Task ForceRefreshAsync()
    {
        var cts = _cancellationTokenSource;
        if (cts == null || cts.IsCancellationRequested) return;

        try
        {
            await _refreshGate.WaitAsync(cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        await RefreshHoldingGateAsync(cts);
        await RunPendingTicksAsync();
    }

    private void CheckTimerStatus()
    {
        lock (_timerLock)
        {
            if (_isActiveScreen && _isAppActive)
            {
                if (_timer == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    // Use a short delay instead of TimeSpan.Zero to avoid
                    // racing with a still-running OnTick from the previous timer
                    var startDelay = Transporters.Any() ? RestartDelay : TimeSpan.Zero;
                    // When auto-refresh is disabled by account settings we still
                    // fire once to load the initial snapshot, but never repeat.
                    var period = _refreshEnabled ? _refreshInterval : Timeout.InfiniteTimeSpan;
                    _timer = _time.CreateTimer(OnTick, null, startDelay, period);
                }
            }
            else
            {
                StopTimer();
            }
        }
    }

    private void StopTimer()
    {
        // Cancel first so in-flight requests stop
        var oldCts = _cancellationTokenSource;
        _cancellationTokenSource = null;
        oldCts?.Cancel();

        _timer?.Dispose();
        _timer = null;

        oldCts?.Dispose();
    }

    private async void OnTick(object? state)
    {
        Interlocked.Exchange(ref _tickPending, 1);
        await RunPendingTicksAsync();
    }

    private async Task RunPendingTicksAsync()
    {
        while (Volatile.Read(ref _tickPending) == 1 && _refreshGate.Wait(0))
        {
            Interlocked.Exchange(ref _tickPending, 0);
            await RefreshHoldingGateAsync(_cancellationTokenSource);
        }
    }

    private async Task RefreshHoldingGateAsync(CancellationTokenSource? screen)
    {
        try
        {
            if (screen == null) return;

            CancellationToken session;
            lock (_sessionLock)
            {
                session = _session.Token;
            }

            using var tick = CancellationTokenSource.CreateLinkedTokenSource(screen.Token, session);
            await RefreshDataAsync(tick.Token, session);
        }
        catch (ObjectDisposedException)
        {
            // CTS was disposed during navigation — safe to ignore
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Applies the account-level refresh settings. The interval is clamped to
    /// a minimum of 10 seconds; when the timer is already running it is
    /// rescheduled with the new cadence. refreshEnabled = false stops the
    /// periodic auto-refresh (manual/forced refresh keeps working).
    /// </summary>
    public void ApplyAccountSettings(bool refreshEnabled, int refreshIntervalSeconds)
    {
        var seconds = Math.Max((int)MinRefreshInterval.TotalSeconds, refreshIntervalSeconds);
        var interval = TimeSpan.FromSeconds(seconds);

        lock (_timerLock)
        {
            _refreshEnabled = refreshEnabled;
            _refreshInterval = interval;

            if (_timer != null)
            {
                if (_refreshEnabled)
                {
                    _timer.Change(_refreshInterval, _refreshInterval);
                }
                else
                {
                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
        }
    }

    // Drops everything tied to the signed-out user so the next sign-in (possibly another account)
    // starts from an empty fleet and re-reads settings and operational status.
    public void ResetSession()
    {
        lock (_sessionLock)
        {
            _session.Cancel();
            _session.Dispose();
            _session = new CancellationTokenSource();

            Transporters = [];
            StatusRules = UnitStatusRules.Default;
            manager.ResetSession();
            Interlocked.Exchange(ref _settingsFetchStarted, 0);
            _settingsRetryDelay = TimeSpan.Zero;
            _settingsRetryAt = DateTimeOffset.MinValue;
            Interlocked.Exchange(ref _accountStatusFetchStarted, 0);
            _accountOperational = true;
            ApplyAccountSettings(true, (int)DefaultRefreshInterval.TotalSeconds);
            WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(Transporters, StatusRules));
            WeakReferenceMessenger.Default.Send(new AccountSuspendedMessage(false));
        }
    }

    private bool PublishIfCurrent(CancellationToken session, Action publish)
    {
        lock (_sessionLock)
        {
            if (session.IsCancellationRequested) return false;
            publish();
            return true;
        }
    }

    // The screens handle the message on the main thread, where sign-out also runs; re-checking there
    // keeps a queued update from landing after the reset.
    private void PostToMainThreadIfCurrent(CancellationToken session, Action action)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!session.IsCancellationRequested) action();
        });

    // Applies the account settings once per session. Until a read succeeds the defaults stay in force;
    // a failed or empty read is retried after one refresh interval, doubling up to
    // MaxSettingsRetryDelay, so a permanent refusal does not hit the Manager on every tick.
    private async Task EnsureAccountSettingsAsync(CancellationToken cancellationToken, CancellationToken session)
    {
        var now = _time.GetUtcNow();
        lock (_sessionLock)
        {
            if (now + SettingsRetryTolerance < _settingsRetryAt) return;
        }

        if (Interlocked.CompareExchange(ref _settingsFetchStarted, 1, 0) != 0)
            return;

        var applied = false;
        var cancelled = false;
        try
        {
            var settings = await manager.GetAccountSettingsAsync(cancellationToken);
            if (settings is { } value && value.AccountId != Guid.Empty)
            {
                // RefreshMapInterval is expressed in seconds
                applied = PublishIfCurrent(session, () =>
                {
                    ApplyAccountSettings(value.RefreshMap, value.RefreshMapInterval);
                    StatusRules = UnitStatusRules.FromOnlineInterval(value.OnlineInterval);
                    _settingsRetryDelay = TimeSpan.Zero;
                    _settingsRetryAt = DateTimeOffset.MinValue;
                });
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch
        {
        }
        finally
        {
            if (!applied)
            {
                if (!cancelled)
                {
                    ScheduleSettingsRetry(now, session);
                }
                Interlocked.Exchange(ref _settingsFetchStarted, 0);
            }
        }
    }

    private void ScheduleSettingsRetry(DateTimeOffset attemptedAt, CancellationToken session)
    {
        TimeSpan interval;
        lock (_timerLock)
        {
            interval = _refreshInterval;
        }

        PublishIfCurrent(session, () =>
        {
            var delay = _settingsRetryDelay == TimeSpan.Zero ? interval : _settingsRetryDelay * 2;
            _settingsRetryDelay = delay < MaxSettingsRetryDelay ? delay : MaxSettingsRetryDelay;
            _settingsRetryAt = attemptedAt + _settingsRetryDelay;
        });
    }

    // Checks the account's operational status once per session. When non-operational, raises a
    // suspension message and suppresses further operational queries. Unknown/failed reads fail open on
    // the client (the backend still fail-closes every data call with ACCOUNT_SUSPENDED).
    private async Task<bool> EnsureAccountOperationalAsync(CancellationToken cancellationToken, CancellationToken session)
    {
        if (!_accountOperational)
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref _accountStatusFetchStarted, 1, 0) != 0)
        {
            return _accountOperational;
        }

        try
        {
            var statusId = await manager.GetAccountStatusAsync(cancellationToken);
            // StatusId 1 (Trial) / 2 (Active) are operational; anything else is not.
            if (statusId.HasValue && statusId.Value != 1 && statusId.Value != 2)
            {
                if (PublishIfCurrent(session, () => _accountOperational = false))
                {
                    PostToMainThreadIfCurrent(session, () =>
                        WeakReferenceMessenger.Default.Send(new AccountSuspendedMessage(true)));
                }
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            // Never completed — allow a retry on the next tick.
            Interlocked.Exchange(ref _accountStatusFetchStarted, 0);
        }
        catch
        {
            // Unknown status — keep operating; the backend remains authoritative.
        }

        return _accountOperational;
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken, CancellationToken session)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Nothing to read until the user is signed in. Staying silent here keeps the
            // once-per-session settings/status reads for the first authenticated tick.
            if (!await authentication.IsAuthenticatedAsync()) return;

            // Block operational queries when the account is non-operational.
            if (!await EnsureAccountOperationalAsync(cancellationToken, session)) return;

            await EnsureAccountSettingsAsync(cancellationToken, session);

            var result = await router.GetDevicePositionsByUserAsync(cancellationToken);

            // The session lapsed while the query was in flight — the sign-in prompt handles it
            if (result.IsUnauthenticated) return;

            if (result.HasError && result.Data is null)
            {
                // Failed read — keep the cached data; only toast when nothing is cached
                ToastIfNothingCached(session);
                return;
            }

            // An empty fleet is a valid result and must reach the screens
            var units = result.Data?.ToList() ?? [];
            UnitStatusRules rules = UnitStatusRules.Default;
            if (PublishIfCurrent(session, () => { Transporters = units; rules = StatusRules; }))
            {
                PostToMainThreadIfCurrent(session, () =>
                    WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(units, rules)));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when screen/app goes inactive or the user signs out during a request
        }
        catch
        {
            ToastIfNothingCached(session);
        }
    }

    private void ToastIfNothingCached(CancellationToken session)
    {
        if (!Transporters.Any())
        {
            PostToMainThreadIfCurrent(session, () =>
                WeakReferenceMessenger.Default.Send(new ToastMessage(localization["Error"], true)));
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_timerLock)
        {
            StopTimer();
        }
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
