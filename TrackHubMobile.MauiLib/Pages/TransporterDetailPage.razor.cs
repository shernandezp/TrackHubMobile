using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Pages;

public partial class TransporterDetailPage(
    IDataRefresh refresh,
    NavigationManager navigationManager) : ActiveScreenComponentBase(refresh, navigationManager), IDisposable
{
    [Parameter]
    public string TransporterIdParam { get; set; } = string.Empty;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool mapInitialized;
    private PositionVm? drawn;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, (_, _) => InvokeAsync(LoadAsync));
    }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        if (Guid.TryParse(TransporterIdParam, out var transporterId))
        {
            await ViewModel.LoadAsync(transporterId);
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        try
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync("trackHubMap.initMap", Array.Empty<object>(), new { interactive = false });
                mapInitialized = true;
            }

            if (mapInitialized && ViewModel.Transporter is { } unit && unit != drawn)
            {
                drawn = unit;
                await JS.InvokeVoidAsync("trackHubMap.updateMarkers",
                    new[] { MapMarker.From(unit, ViewModel.StatusRules) }, true);
            }
        }
        catch
        {
            // JS interop can fail if the page is navigated away quickly
        }
    }

    private async Task GoBackAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("history.back");
        }
        catch (JSException)
        {
        }
    }

    public new void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<DataRefreshedMessage>(this);
        try
        {
            _ = JS.InvokeVoidAsync("trackHubMap.destroyMap");
        }
        catch
        {
            // Dispose should not throw
        }
        base.Dispose();
    }
}
