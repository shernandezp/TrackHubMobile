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

using System.Globalization;
using System.Xml;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;

namespace TrackHubMobile.Pages;

public partial class PositionHistory : IDisposable
{
    private const string StoredHistoryFeatureKey = "gps.positionHistory";
    private const string StoredSource = "STORED";
    private const int MaxRangeDays = 31;

    [Parameter]
    public string TransporterIdParam { get; set; } = string.Empty;

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IRouter Router { get; set; } = default!;
    [Inject] private IManager Manager { get; set; } = default!;

    // Default range: last 24 hours. These are bound to a local-time
    // datetime-local picker, so they carry the device-local offset; they are
    // converted to their absolute instant when passed to the API.
    private DateTimeOffset FromDate { get; set; } = DateTimeOffset.Now.AddDays(-1);
    private DateTimeOffset ToDate { get; set; } = DateTimeOffset.Now;

    private Guid transporterId;
    private bool hasStoredHistoryFeature;
    private bool useStoredSource;
    private bool isLoading;
    private bool hasSearched;
    private bool showRangeError;
    private bool featureDisabled;
    private bool hasError;
    private bool mapInitialized;
    private Guid? selectedTripId;
    private List<TripVm> trips = [];

    protected override void OnParametersSet()
    {
        Guid.TryParse(TransporterIdParam, out transporterId);
    }

    protected override async Task OnInitializedAsync()
    {
        // The source switch is only offered when the account has the
        // gps.positionHistory feature; on any failure fall back to provider-only.
        try
        {
            var settings = await Manager.GetAccountSettingsAsync(CancellationToken.None);
            if (settings.HasValue && settings.Value.AccountId != Guid.Empty)
            {
                var features = await Manager.GetAccountFeaturesAsync(settings.Value.AccountId, CancellationToken.None);
                hasStoredHistoryFeature = features.Any(f =>
                    string.Equals(f.FeatureKey, StoredHistoryFeatureKey, StringComparison.OrdinalIgnoreCase) && f.Enabled);
            }
        }
        catch
        {
            hasStoredHistoryFeature = false;
        }

        // Default source: TrackHub (STORED) when available, otherwise GPS provider
        useStoredSource = hasStoredHistoryFeature;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await JS.InvokeVoidAsync("trackHubMap.initMap", Array.Empty<object>());
                mapInitialized = true;
            }
            catch
            {
                // JS interop can fail if the page is navigated away quickly
            }
        }
    }

    private void GoBack()
    {
        Navigation.NavigateTo($"/transporter/{TransporterIdParam}");
    }

    private void SetSource(bool stored)
    {
        useStoredSource = stored && hasStoredHistoryFeature;
    }

    // Trip type 1 marks a stop segment; everything else is a moving trip
    private static bool IsMoving(TripVm trip) => trip.Type != 1;

    private async Task SearchAsync()
    {
        showRangeError = false;
        featureDisabled = false;
        hasError = false;

        if (transporterId == Guid.Empty)
        {
            return;
        }

        if (ToDate <= FromDate || (ToDate - FromDate) > TimeSpan.FromDays(MaxRangeDays))
        {
            showRangeError = true;
            return;
        }

        isLoading = true;
        hasSearched = false;
        selectedTripId = null;
        trips = [];
        StateHasChanged();

        try
        {
            var source = hasStoredHistoryFeature && useStoredSource ? StoredSource : null;
            var result = await Router.GetTripsByTransporterAsync(
                transporterId,
                FromDate,
                ToDate,
                source,
                CancellationToken.None);

            if (result.IsFeatureDisabled)
            {
                featureDisabled = true;
            }
            else if (result.HasError && result.Data is null)
            {
                hasError = true;
            }
            else
            {
                trips = result.Data?.ToList() ?? [];
            }
        }
        catch
        {
            // Offline or transport failure — show a localized error banner
            hasError = true;
        }
        finally
        {
            hasSearched = true;
            isLoading = false;
        }

        await ClearTrackAsync();
    }

    private async Task SelectTripAsync(TripVm trip)
    {
        selectedTripId = trip.TripId;

        if (!IsMoving(trip) || trip.Points is null || trip.Points.Count == 0)
        {
            await ClearTrackAsync();
            return;
        }

        if (!mapInitialized)
        {
            return;
        }

        var points = trip.Points
            .Select(p => new
            {
                lat = p.Latitude,
                lng = p.Longitude,
                speed = p.Speed,
                dateTime = p.DeviceDateTime.ToString("o")
            })
            .ToArray();

        try
        {
            await JS.InvokeVoidAsync("trackHubMap.drawTrack", (object)points, new { });
        }
        catch
        {
            // JS interop can fail during navigation
        }
    }

    private async Task ClearTrackAsync()
    {
        if (!mapInitialized)
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("trackHubMap.clearTrack");
        }
        catch
        {
            // JS interop can fail during navigation
        }
    }

    // Duration usually arrives as an ISO 8601 duration string (e.g. "PT1H5M30S",
    // HotChocolate's default TimeSpan serialization), but tolerate the .NET
    // "c" format ("01:05:30") as well in case the scalar is configured differently.
    private static string FormatDuration(TripVm trip)
    {
        TimeSpan duration;
        if (!string.IsNullOrEmpty(trip.Duration))
        {
            if (!TimeSpan.TryParse(trip.Duration, CultureInfo.InvariantCulture, out duration))
            {
                try
                {
                    duration = XmlConvert.ToTimeSpan(trip.Duration);
                }
                catch
                {
                    duration = trip.To - trip.From;
                }
            }
        }
        else
        {
            duration = trip.To - trip.From;
        }

        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} h {duration.Minutes} min"
            : $"{duration.Minutes} min";
    }

    public void Dispose()
    {
        try
        {
            _ = JS.InvokeVoidAsync("trackHubMap.destroyMap");
        }
        catch
        {
            // Dispose should not throw
        }
    }
}
