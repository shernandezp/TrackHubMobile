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

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;

namespace TrackHubMobile.Pages;

public partial class TransporterMap : ActiveScreenComponentBase, IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private TransporterMapViewModel ViewModel { get; set; } = default!;
    [Inject] private IRouter Router { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "transporterId")]
    public string? TransporterIdParam { get; set; }

    private bool isLoading = true;
    private bool mapInitialized;

    public TransporterMap(IDataRefresh refresh, NavigationManager navigationManager)
        : base(refresh, navigationManager)
    {
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, HandleRefreshMessage);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            try
            {
                await JS.InvokeVoidAsync("trackHubMap.initMap", Array.Empty<object>(), BuildMapLabels());
                mapInitialized = true;
                await LoadInitialDataAsync();
            }
            catch
            {
                // JS interop can fail if the page is navigated away quickly
            }
        }
    }

    // Localized strings for the popups map.js builds on its own
    private object BuildMapLabels() => new
    {
        moving = LRM["InMovement"],
        stopped = LRM["Stopped"],
        offline = LRM["Offline"],
        justNow = LRM["JustNow"],
        minutesAgo = LRM["MinutesAgo"],
        hoursAgo = LRM["HoursAgo"],
        daysAgo = LRM["DaysAgo"],
        accOn = LRM["AccOn"],
        accOff = LRM["AccOff"]
    };

    private async Task LoadInitialDataAsync()
    {
        isLoading = true;
        StateHasChanged();

        await ViewModel.LoadDataAsync();

        if (ViewModel.Transporters is not null)
        {
            if (!string.IsNullOrEmpty(TransporterIdParam) &&
                Guid.TryParse(TransporterIdParam, out var tid))
            {
                await FocusOnSingleUnit(tid, preserveView: false);
            }
            else
            {
                await UpdateMapMarkers(ViewModel.Transporters, fitView: true);
            }
        }

        isLoading = false;
        StateHasChanged();
    }

    private async Task FocusOnSingleUnit(Guid transporterId, bool preserveView)
    {
        try
        {
            var device = await Router.GetDeviceAsync(transporterId, CancellationToken.None);
            if (device.DeviceDateTime != default)
            {
                var jsObj = MapPositionToJs(device);
                await JS.InvokeVoidAsync("trackHubMap.focusSingleUnit", jsObj, preserveView);
            }
        }
        catch
        {
            // If single unit fetch fails, show all markers instead
            if (!preserveView && ViewModel.Transporters is not null)
            {
                await UpdateMapMarkers(ViewModel.Transporters, fitView: true);
            }
        }
    }

    private async Task UpdateMapMarkers(IEnumerable<PositionVm> transporters, bool fitView)
    {
        if (!mapInitialized) return;

        try
        {
            var jsPositions = transporters.Select(MapPositionToJs).ToArray();
            await JS.InvokeVoidAsync("trackHubMap.updateMarkers", (object)jsPositions, fitView);
        }
        catch
        {
            // JS interop can fail during navigation
        }
    }

    private void HandleRefreshMessage(object recipient, DataRefreshedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (string.IsNullOrEmpty(TransporterIdParam))
            {
                // Keep the user's pan/zoom on periodic refreshes
                await UpdateMapMarkers(message.Value, fitView: false);
            }
            else if (mapInitialized && Guid.TryParse(TransporterIdParam, out var tid))
            {
                // Single-unit focus keeps tracking the unit as it moves
                await FocusOnSingleUnit(tid, preserveView: true);
            }
        });
    }

    private static object MapPositionToJs(PositionVm p) => new
    {
        lat = p.Latitude,
        lng = p.Longitude,
        name = p.DeviceName,
        speed = p.Speed,
        dateTime = p.DeviceDateTime.ToString("o"),
        transporterType = p.TransporterType,
        course = p.Course ?? 0,
        address = p.Address ?? "",
        city = p.City ?? "",
        state = p.State ?? "",
        ignition = p.Attributes?.Ignition,
        transporterId = p.TransporterId.ToString()
    };

    public new void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<DataRefreshedMessage>(this);
        try
        {
            _ = JS.InvokeVoidAsync("trackHubMap.destroyMap");
        }
        catch
        {
            // Dispose should not throw
        }
        base.Dispose();
    }
}