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

using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components;
using TrackHubMobile.Interfaces.Services;

namespace TrackHubMobile.Pages
{
    public abstract class ActiveScreenComponentBase(IDataRefresh refresh, NavigationManager navigation) : ComponentBase, IDisposable
    {

        protected NavigationManager Navigation { get; } = navigation;
        private bool _isCurrentScreen = false;

        protected override void OnInitialized()
        {
            Navigation.LocationChanged += HandleLocationChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _isCurrentScreen = true;
                refresh.SetScreenActive(true);
            }
        }

        private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            if (_isCurrentScreen)
            {
                _isCurrentScreen = false;
                refresh.SetScreenActive(false);
            }
        }

        public void Dispose()
        {
            Navigation.LocationChanged -= HandleLocationChanged;
        }
    }
}
