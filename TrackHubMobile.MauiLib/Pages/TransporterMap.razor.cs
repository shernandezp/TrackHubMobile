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
                await JS.InvokeVoidAsync("trackHubMap.initMap", Array.Empty<object>());
                mapInitialized = true;
                await LoadInitialDataAsync();
            }
            catch
            {
                // JS interop can fail if the page is navigated away quickly
            }
        }
    }

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
                await FocusOnSingleUnit(tid);
            }
            else
            {
                await UpdateMapMarkers(ViewModel.Transporters);
            }
        }

        isLoading = false;
        StateHasChanged();
    }

    private async Task FocusOnSingleUnit(Guid transporterId)
    {
        try
        {
            var device = await Router.GetDeviceAsync(transporterId, CancellationToken.None);
            if (device.DeviceDateTime != default)
            {
                var jsObj = MapPositionToJs(device);
                await JS.InvokeVoidAsync("trackHubMap.focusSingleUnit", jsObj);
            }
        }
        catch
        {
            // If single unit fetch fails, show all markers instead
            if (ViewModel.Transporters is not null)
            {
                await UpdateMapMarkers(ViewModel.Transporters);
            }
        }
    }

    private async Task UpdateMapMarkers(IEnumerable<PositionVm> transporters)
    {
        if (!mapInitialized) return;

        try
        {
            var jsPositions = transporters.Select(MapPositionToJs).ToArray();
            await JS.InvokeVoidAsync("trackHubMap.updateMarkers", (object)jsPositions);
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
                await UpdateMapMarkers(message.Value);
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