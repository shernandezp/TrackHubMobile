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
using Microsoft.AspNetCore.Components;
using TrackHubMobile.Models;

namespace TrackHubMobile.Shared;

public partial class TransporterActions
{
    [Parameter, EditorRequired]
    public PositionVm Transporter { get; set; }

    [Parameter]
    public bool ShowDetailsButton { get; set; } = true;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task OpenInGoogleMaps()
    {
        var lat = Transporter.Latitude.ToString(CultureInfo.InvariantCulture);
        var lng = Transporter.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"https://www.google.com/maps?q={lat},{lng}";
        await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.External);
    }

    private void ShowOnMap()
    {
        Navigation.NavigateTo($"/mapview?transporterId={Transporter.TransporterId}");
    }

    private async Task ShareViaWhatsApp()
    {
        var lat = Transporter.Latitude.ToString(CultureInfo.InvariantCulture);
        var lng = Transporter.Longitude.ToString(CultureInfo.InvariantCulture);
        var mapsLink = $"https://www.google.com/maps?q={lat},{lng}";
        var text = $"{Transporter.DeviceName} - {Transporter.DeviceDateTime.ToLocalTime():g}\n{mapsLink}";
        var encoded = Uri.EscapeDataString(text);
        var url = $"https://wa.me/?text={encoded}";
        await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.External);
    }

    private void ViewDetails()
    {
        Navigation.NavigateTo($"/transporter/{Transporter.TransporterId}");
    }

    private void ViewHistory()
    {
        Navigation.NavigateTo($"/transporter/{Transporter.TransporterId}/history");
    }
}
