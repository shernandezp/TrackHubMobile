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

namespace TrackHubMobile.Shared;

public partial class TransporterDetail
{
    [Parameter]
    public Guid? TransporterId { get; set; }

    private string? alertStyle;

    protected override void OnInitialized()
    {
        alertStyle = appInfo.RequestedTheme switch
        {
            AppTheme.Dark => "card bg-dark text-white mt-3",
            _ => "card bg-light text-dark mt-3"
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        if (TransporterId.HasValue)
        {
            await ViewModel.OnTransporterSelected(TransporterId.Value);
        }
    }
}
