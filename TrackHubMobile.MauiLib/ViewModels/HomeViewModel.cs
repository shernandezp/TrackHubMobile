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
    private int total;
    [ObservableProperty]
    private int inMovement;
    [ObservableProperty]
    private int stopped;
    [ObservableProperty]
    private int offline;
    [ObservableProperty]
    private int speeding;
    [ObservableProperty]
    private int ignitionOn;
    [ObservableProperty]
    private double averageSpeed;
    [ObservableProperty]
    private DateTimeOffset? lastGlobalUpdate;

    public HomeViewModel() : base("Dashboard")
    {
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, HandleRefreshMessage);
    }

    public Action? OnUpdated { get; set; }

    private void HandleRefreshMessage(object recipient, DataRefreshedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var transporters = message.Value.ToList();
            var offlineThreshold = DateTimeOffset.UtcNow.AddHours(-2);

            Total = transporters.Count;
            InMovement = transporters.Count(t => t.Speed > 0);
            Offline = transporters.Count(t => t.DeviceDateTime < offlineThreshold);
            Stopped = Total - InMovement - Offline;
            Speeding = transporters.Count(t => t.Speed > 80);
            IgnitionOn = transporters.Count(t => t.Attributes?.Ignition == true);
            AverageSpeed = transporters.Count > 0
                ? Math.Round(transporters.Average(t => t.Speed), 1)
                : 0;
            LastGlobalUpdate = transporters.Count > 0
                ? transporters.Max(t => t.DeviceDateTime)
                : null;

            OnUpdated?.Invoke();
        });
    }
}
