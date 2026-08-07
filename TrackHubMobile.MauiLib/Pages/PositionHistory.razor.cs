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
    private const string DateFormat = "yyyy-MM-dd";

    // A day at a time: anything wider is too much data to pull onto a phone, and it is
    // also how drivers and dispatchers read a route.
    private enum RangePreset
    {
        Last24Hours,
        Today,
        Yesterday,
        Day
    }

    [Parameter]
    public string TransporterIdParam { get; set; } = string.Empty;

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IRouter Router { get; set; } = default!;
    [Inject] private IManager Manager { get; set; } = default!;

    // The chosen day is held as the raw text of the date input, never as a bound
    // DateTimeOffset: a native picker emits partial values while the user edits, and a
    // typed binding rejects those and rewrites the field, which is what made the picker
    // snap back to the previous day. Text round-trips untouched and is only interpreted
    // when a search runs.
    private string dayText = string.Empty;

    private Guid transporterId;
    private RangePreset selectedPreset = RangePreset.Last24Hours;
    private bool hasStoredHistoryFeature;
    private bool useStoredSource;
    private bool isLoading;
    private bool hasSearched;
    private bool showRangeError;
    private bool featureDisabled;
    private bool hasError;
    private bool mapInitialized;
    private bool showFilters;
    private bool sheetExpanded = true;
    private bool needsMapResize;
    private Guid? selectedTripId;
    private List<TripVm> trips = [];

    private string RangeSummary => selectedPreset switch
    {
        RangePreset.Last24Hours => LRM["Last24Hours"],
        RangePreset.Today => LRM["Today"],
        RangePreset.Yesterday => LRM["Yesterday"],
        _ => TryParseDay(out var day) ? day.ToString("d") : LRM["ChooseDay"]
    };

    // No future days to pick: there is nothing to show there.
    private static string MaxDayText => DateTime.Today.ToString(DateFormat, CultureInfo.InvariantCulture);

    protected override void OnParametersSet()
    {
        Guid.TryParse(TransporterIdParam, out transporterId);
    }

    protected override async Task OnInitializedAsync()
    {
        // OnParametersSet only runs after this method completes, so the id must be
        // parsed here for the initial search to have it.
        Guid.TryParse(TransporterIdParam, out transporterId);

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

        // Land on the last 24 hours already drawn; the filters stay out of the way
        // until the user opens them.
        await SearchAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Controls move to the top edge so the trips sheet never covers them.
                await JS.InvokeVoidAsync("trackHubMap.initMap",
                    Array.Empty<object>(),
                    null,
                    new { zoomPosition = "topright", attributionPosition = "topleft" });
                mapInitialized = true;
            }
            catch
            {
                // JS interop can fail if the page is navigated away quickly
            }

            // The initial search may have finished before the map existed
            await DrawAllAsync();
            return;
        }

        if (needsMapResize)
        {
            needsMapResize = false;
            await InvokeMapAsync("trackHubMap.resize");
        }
    }

    private void GoBack()
    {
        Navigation.NavigateTo($"/transporter/{TransporterIdParam}");
    }

    private async Task SetSourceAsync(bool stored)
    {
        var next = stored && hasStoredHistoryFeature;
        if (next == useStoredSource)
        {
            return;
        }

        useStoredSource = next;
        await SearchAsync();
    }

    private void ToggleFilters()
    {
        showFilters = !showFilters;
        needsMapResize = true;
    }

    // The sheet floats over the map, so opening or closing it only changes how much of
    // the track has to fit above it.
    private async Task ToggleSheetAsync()
    {
        sheetExpanded = !sheetExpanded;
        await InvokeMapAsync("trackHubMap.refit", new { bottomInsetRatio = BottomInsetRatio });
    }

    // Trip type 1 marks a stop segment; everything else is a moving trip
    private static bool IsMoving(TripVm trip) => trip.Type != 1;

    private async Task ApplyPresetAsync(RangePreset preset)
    {
        selectedPreset = preset;
        showRangeError = false;

        // Picking a day waits for the date input; the fixed options search on the tap.
        if (preset == RangePreset.Day && !TryParseDay(out _))
        {
            return;
        }

        await SearchAsync();
    }

    // Whatever the picker emits is kept verbatim, including a half-finished or cleared
    // value; it is only judged when a search runs.
    private async Task OnDayChangedAsync(ChangeEventArgs args)
    {
        dayText = args.Value?.ToString() ?? string.Empty;
        showRangeError = false;
        await SearchAsync();
    }

    private bool TryParseDay(out DateTime day)
        => DateTime.TryParseExact(dayText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    // Every option resolves to at most one day of data.
    private bool TryBuildRange(out DateTimeOffset from, out DateTimeOffset to)
    {
        var now = DateTime.Now;
        switch (selectedPreset)
        {
            case RangePreset.Today:
                return BuildRange(now.Date, now.Date.AddDays(1), out from, out to);
            case RangePreset.Yesterday:
                return BuildRange(now.Date.AddDays(-1), now.Date, out from, out to);
            case RangePreset.Day:
                return TryParseDay(out var day)
                    ? BuildRange(day, day.AddDays(1), out from, out to)
                    : Fail(out from, out to);
            default:
                return BuildRange(now.AddDays(-1), now, out from, out to);
        }
    }

    private static bool BuildRange(DateTime from, DateTime to, out DateTimeOffset rangeFrom, out DateTimeOffset rangeTo)
    {
        rangeFrom = ToLocalOffset(from);
        rangeTo = ToLocalOffset(to);
        return true;
    }

    private static bool Fail(out DateTimeOffset from, out DateTimeOffset to)
    {
        from = default;
        to = default;
        return false;
    }

    private static DateTimeOffset ToLocalOffset(DateTime value)
        => new(value, TimeZoneInfo.Local.GetUtcOffset(value));

    private async Task SearchAsync()
    {
        showRangeError = false;
        featureDisabled = false;
        hasError = false;

        if (transporterId == Guid.Empty)
        {
            return;
        }

        if (!TryBuildRange(out var from, out var to))
        {
            showRangeError = true;
            showFilters = true;
            return;
        }

        isLoading = true;
        hasSearched = false;
        selectedTripId = null;
        trips = [];
        await ClearTrackAsync();
        StateHasChanged();

        try
        {
            var source = hasStoredHistoryFeature && useStoredSource ? StoredSource : null;
            var result = await Router.GetTripsByTransporterAsync(
                transporterId,
                from,
                to,
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

        // The map is the point of the screen, so the filters fold away once they have
        // been used and the results take over the sheet.
        showFilters = false;
        sheetExpanded = true;
        needsMapResize = true;

        await DrawAllAsync();
    }

    // Draws the whole range: every moving trip as its own polyline plus a dot per stop.
    private async Task DrawAllAsync()
    {
        selectedTripId = null;

        if (!mapInitialized)
        {
            return;
        }

        var segments = trips
            .Where(t => IsMoving(t) && t.Points is { Count: > 0 })
            .Select(t => t.Points!.Select(ToMapPoint).ToArray())
            .ToArray();

        var stops = trips
            .Where(t => !IsMoving(t) && t.Points is { Count: > 0 })
            .Select(t => ToMapPoint(t.Points![0]))
            .ToArray();

        if (segments.Length == 0 && stops.Length == 0)
        {
            await ClearTrackAsync();
            return;
        }

        await InvokeMapAsync("trackHubMap.drawTracks", segments, stops, new { bottomInsetRatio = BottomInsetRatio });
    }

    private async Task SelectTripAsync(TripVm trip)
    {
        selectedTripId = trip.TripId;

        // Give the map the screen once a trip is picked; the sheet is one tap away again.
        sheetExpanded = false;

        if (trip.Points is null || trip.Points.Count == 0)
        {
            await ClearTrackAsync();
            return;
        }

        // A stop segment renders as a single location marker; a moving trip as the full track
        var tripPoints = IsMoving(trip) ? trip.Points : trip.Points.Take(1);
        var points = tripPoints.Select(ToMapPoint).ToArray();

        await InvokeMapAsync("trackHubMap.drawTrack", points, new { bottomInsetRatio = CollapsedInsetRatio });
    }

    private double BottomInsetRatio => sheetExpanded ? ExpandedInsetRatio : CollapsedInsetRatio;

    // Keeps the fitted track clear of the sheet: roughly its expanded height, or just
    // the collapsed handle.
    private const double ExpandedInsetRatio = 0.5;
    private const double CollapsedInsetRatio = 0.12;

    private static object ToMapPoint(TripPointVm point)
        => new
        {
            lat = point.Latitude,
            lng = point.Longitude,
            speed = point.Speed,
            dateTime = point.DeviceDateTime.ToString("o")
        };

    private Task ClearTrackAsync() => InvokeMapAsync("trackHubMap.clearTrack");

    private async Task InvokeMapAsync(string identifier, params object?[] args)
    {
        if (!mapInitialized)
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync(identifier, args);
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
