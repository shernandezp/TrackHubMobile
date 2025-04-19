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

using TrackHubMobile.Messages;

namespace TrackHubMobile.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    [ObservableProperty]
    public int total = 0;
    [ObservableProperty]
    public int inMovement = 0;
    [ObservableProperty]
    public int offline = 0;
    [ObservableProperty]
    public int speeding = 0;

    public HomeViewModel() : base("Dashboard")
    {
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, HandleRefreshMessage);
    }
    public Action? OnUpdated { get; set; }

    private void HandleRefreshMessage(object recipient, DataRefreshedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var transporters = message.Value;
            Total = transporters.Count();
            InMovement = transporters.Count(t => t.Speed > 0);
            var offlineTime = DateTime.Now.AddHours(-2);
            Offline = transporters.Count(t => t.DeviceDateTime < offlineTime);
            Speeding = transporters.Count(t => t.Speed > 80);

            OnUpdated?.Invoke();
        });
    }
}
