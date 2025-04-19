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

using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;

namespace TrackHubMobile.Services;

public class DataRefresh(IRouter router) : IAsyncDisposable, IDataRefresh
{
    private Timer? _timer;
    private int _counter = 0;
    private bool _isActiveScreen = false;
    private bool _isAppActive = true;
    private CancellationTokenSource? _cancellationTokenSource;

    public IEnumerable<PositionVm> Transporters { get; set; } = [];

    public void SetScreenActive(bool isActive)
    {
        _isActiveScreen = isActive;
        CheckTimerStatus();
    }

    public async Task SetAppActive(bool isActive, bool forceRefresh = false)
    {
        _isAppActive = isActive;
        CheckTimerStatus();
        if (forceRefresh)
        {
            await RefreshDataAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
        }
    }

    private void CheckTimerStatus()
    {
        if (_isActiveScreen && _isAppActive)
        {
            if (_timer == null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            }
        }
        else
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick(object? state)
    {
        _ = TickAsync();
    }

    private async Task TickAsync()
    {
        try
        {
            _counter++;

            if (_counter >= 6) // 30 seconds
            {
                _counter = 0;
                await RefreshDataAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Tick: {ex.Message}");
        }
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            Transporters = await router.GetDevicePositionsByUserAsync(cancellationToken);
            WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(Transporters));
        }
        catch
        {
            // Notify UI
            // Log errors
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _timer?.Dispose();
        _timer = null;
    }
}
