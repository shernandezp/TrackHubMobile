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
using TrackHubMobile.Models;

namespace TrackHubMobile.ViewModels;

public partial class TransporterDetailViewModel(IRouter router, IDataRefresh dataRefresh) : BaseViewModel
{
    [ObservableProperty]
    private PositionVm? transporter;
    [ObservableProperty]
    private bool isRefreshing;
    [ObservableProperty]
    private bool hasError;

    public async Task OnTransporterSelected(Guid transporterId)
    {
        HasError = false;
        IsRefreshing = true;

        // Show cached basic data immediately while full details load
        var cached = dataRefresh.Transporters.FirstOrDefault(t => t.TransporterId == transporterId);
        if (cached.TransporterId != Guid.Empty)
        {
            Transporter = cached;
        }

        try
        {
            var result = await router.GetDeviceAsync(transporterId, CancellationToken.None);
            if (result.DeviceDateTime != default)
            {
                Transporter = result;
            }
            // If API returned default but we have cached data, keep showing it
        }
        catch
        {
            // API failed — keep showing cached data if available
            if (Transporter is null)
            {
                HasError = true;
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
