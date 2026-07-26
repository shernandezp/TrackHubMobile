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

using CommunityToolkit.Maui;
using TrackHubMobile.Helpers;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Services;
using TrackHubMobile.ViewModels;
using TrackHubMobile.Views;

namespace TrackHubMobile;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                   fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
               });

        //ViewModels
        builder.Services.AddSingleton<AboutViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<NavMenuViewModel>();
        builder.Services.AddSingleton<TransporterDetailViewModel>();
        builder.Services.AddSingleton<TransporterListViewModel>();
        builder.Services.AddSingleton<TransporterMapViewModel>();

        //Pages
        // Singleton will not allow NavigationManager to be injected
        builder.Services.AddSingleton<MainPage>();

        builder.Services.AddHttpClient("GraphQL", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient("Auth");
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(AppInfo.Current);

        //Helpers
        builder.Services.AddSingleton<IGraphQLReader, GraphQLReader>();
        builder.Services.AddSingleton<ILocalizationResourceManager, LocalizationResourceManager>();
        builder.Services.AddSingleton<IToastDisplay, ToastDisplay>();
        builder.Services.AddSingleton<ITransporterHelper, TransporterHelper>();

        //Services
        builder.Services.AddSingleton<IAuthentication, Authentication>();
        builder.Services.AddSingleton<IDataRefresh, DataRefresh>();
        builder.Services.AddSingleton<IManager, Manager>();
        builder.Services.AddSingleton<IRouter, Router>();
        builder.Services.AddSingleton<IStorage, Storage>();

        var app = builder.Build();
        ServiceHelper.Services = app.Services;

        return app;
    }
}
