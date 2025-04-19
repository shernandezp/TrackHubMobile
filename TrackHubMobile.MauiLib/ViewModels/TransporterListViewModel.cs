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

public partial class TransporterListViewModel(IRouter router, IDataRefresh dataRefresh) : BaseViewModel
{
    [ObservableProperty]
    private IEnumerable<PositionVm>? transporters = null;
    [ObservableProperty]
    private bool isRefreshing;

    public async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        Transporters = null;
        IsRefreshing = true;
        try
        {
            Transporters = await router.GetDevicePositionsByUserAsync(cancellationToken);
            dataRefresh.Transporters = Transporters;
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
