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

using TrackHubMobile.ViewModels;

namespace TrackHubMobile.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel viewModel;
    private bool initialized;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // The page reappears whenever the activity comes back to the foreground
        // (including after the sign-in browser closes), so only the first pass
        // starts the flow. Later retries are driven by App.OnResume.
        if (initialized)
        {
            return;
        }
        initialized = true;

        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception)
        {
            // A failed sign-in already notified the user; never take the app down here
        }
    }

}
