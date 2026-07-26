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

public class DataRefresh(IRouter router, IManager manager, ILocalizationResourceManager localization) : IAsyncDisposable, IDataRefresh
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromMilliseconds(500);

    private Timer? _timer;
    private bool _isActiveScreen;
    private bool _isAppActive = true;
    private int _isRefreshing;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _timerLock = new();

    // Account-settings-driven refresh configuration (defaults: enabled, 30 s)
    private TimeSpan _refreshInterval = DefaultRefreshInterval;
    private bool _refreshEnabled = true;
    private int _settingsFetchStarted;

    // Account operational-status gating: once a non-operational status is observed,
    // operational queries are suppressed and a suspension message is raised.
    private int _accountStatusFetchStarted;
    private volatile bool _accountOperational = true;

    public IEnumerable<PositionVm> Transporters { get; private set; } = [];

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

    public async Task ForceRefreshAsync()
    {
        var cts = _cancellationTokenSource;
        if (cts == null || cts.IsCancellationRequested) return;

        try
        {
            await RefreshDataAsync(cts.Token);
        }
        catch (ObjectDisposedException)
        {
            // CTS was disposed during navigation — safe to ignore
        }
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
                    _timer = new Timer(OnTick, null, startDelay, period);
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

        // Reset reentrancy guard so the next timer start isn't blocked
        Interlocked.Exchange(ref _isRefreshing, 0);

        // Dispose CTS after resetting the guard to avoid ObjectDisposedException
        // while OnTick is still reading the token
        oldCts?.Dispose();
    }

    private async void OnTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
            return;

        try
        {
            var cts = _cancellationTokenSource;
            if (cts == null || cts.IsCancellationRequested) return;

            await RefreshDataAsync(cts.Token);
        }
        catch (ObjectDisposedException)
        {
            // CTS was disposed during navigation — safe to ignore
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshing, 0);
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

    // Fetches account settings once per session; falls back silently
    // to the 30 s default when the Manager call fails or returns nothing.
    private async Task EnsureAccountSettingsAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _settingsFetchStarted, 1, 0) != 0)
            return;

        try
        {
            var settings = await manager.GetAccountSettingsAsync(cancellationToken);
            if (settings.HasValue && settings.Value.AccountId != Guid.Empty)
            {
                // RefreshMapInterval is expressed in seconds
                ApplyAccountSettings(settings.Value.RefreshMap, settings.Value.RefreshMapInterval);
            }
        }
        catch (OperationCanceledException)
        {
            // The attempt never completed — allow a retry on the next session tick
            Interlocked.Exchange(ref _settingsFetchStarted, 0);
        }
        catch
        {
            // Keep the 30 s defaults when the settings cannot be retrieved
        }
    }

    // Checks the account's operational status once per session. When non-operational, raises a
    // suspension message and suppresses further operational queries. Unknown/failed reads fail open on
    // the client (the backend still fail-closes every data call with ACCOUNT_SUSPENDED).
    private async Task<bool> EnsureAccountOperationalAsync(CancellationToken cancellationToken)
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
                _accountOperational = false;
                MainThread.BeginInvokeOnMainThread(() =>
                    WeakReferenceMessenger.Default.Send(new AccountSuspendedMessage(true)));
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

    private async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Block operational queries when the account is non-operational.
            if (!await EnsureAccountOperationalAsync(cancellationToken)) return;

            await EnsureAccountSettingsAsync(cancellationToken);

            var result = await router.GetDevicePositionsByUserAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            if (result.HasError && result.Data is null)
            {
                // Failed read — keep the cached data; only toast when nothing is cached
                if (!Transporters.Any())
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                        WeakReferenceMessenger.Default.Send(new ToastMessage(localization["Error"], true)));
                }
                return;
            }

            // An empty fleet is a valid result and must reach the screens
            Transporters = result.Data ?? [];
            WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(Transporters));
        }
        catch (OperationCanceledException)
        {
            // Expected when screen/app goes inactive during a request
        }
        catch
        {
            // Only show error if we have no cached data at all
            if (!Transporters.Any())
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    WeakReferenceMessenger.Default.Send(new ToastMessage(localization["Error"], true));
                });
            }
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
