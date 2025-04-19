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
using TrackHubMobile.Interfaces.Services;

namespace TrackHubMobile.Pages;

public partial class Home(
    IDataRefresh refresh,
    NavigationManager navigationManager) : ActiveScreenComponentBase(refresh, navigationManager)
{

    private void NavigateToListView()
    {
        // Replace with actual navigation logic
        Navigation.NavigateTo("/listview");
    }

    private void NavigateToMapView()
    {
        // Replace with actual navigation logic
        Navigation.NavigateTo("/counter");
    }

    protected override void OnInitialized()
    {
        ViewModel.OnUpdated = StateHasChanged;
    }

}
