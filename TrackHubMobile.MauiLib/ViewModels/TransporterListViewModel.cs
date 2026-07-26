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

public partial class TransporterListViewModel(IDataRefresh dataRefresh) : BaseViewModel
{
    [ObservableProperty]
    private IEnumerable<PositionVm>? transporters = null;
    [ObservableProperty]
    private bool isRefreshing;
    [ObservableProperty]
    private PositionVm? selectedTransporter;
    [ObservableProperty]
    private string searchText = string.Empty;

    public IEnumerable<PositionVm>? FilteredTransporters =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Transporters
            : Transporters?.Where(t =>
                t.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public async Task LoadDataAsync()
    {
        var existing = dataRefresh.Transporters;
        if (existing.Any())
        {
            Transporters = existing;
            return;
        }

        IsRefreshing = true;
        try
        {
            await dataRefresh.ForceRefreshAsync();
            Transporters = dataRefresh.Transporters;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void UpdateFromRefresh(IEnumerable<PositionVm> transporters)
    {
        Transporters = transporters;
        OnPropertyChanged(nameof(FilteredTransporters));
    }

    public void OnRowClick(PositionVm transporter)
    {
        if (SelectedTransporter?.TransporterId == transporter.TransporterId)
        {
            SelectedTransporter = null;
        }
        else
        {
            SelectedTransporter = transporter;
        }
    }

    public void OnSearchChanged(string value)
    {
        SearchText = value;
        OnPropertyChanged(nameof(FilteredTransporters));
    }
}
