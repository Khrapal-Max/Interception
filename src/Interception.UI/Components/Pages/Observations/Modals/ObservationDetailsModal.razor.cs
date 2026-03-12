//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Modals;

public partial class ObservationDetailsModal : ComponentBase
{
    [Inject] public IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    [Parameter] public Guid ObservationId { get; set; }
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected ObservationDetailsDto? _dto;
    protected bool _loading;
    private Guid _loadedObservationId;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen)
            return;

        if (_dto is null || _loadedObservationId != ObservationId)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _dto = await RegistryService.GetByIdAsync(ObservationId, CancellationToken.None);
            _loadedObservationId = ObservationId;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CloseAsync()
    {
        if (OnClose.HasDelegate)
            await OnClose.InvokeAsync();
    }

    private async Task OpenParticipantAnalyticsAsync(Guid participantId)
    {
        await CloseAsync();
        Navigation.NavigateTo($"/analytics/participants/{participantId}");
    }

    private async Task OpenClusterAnalyticsAsync(Guid? clusterId)
    {
        if (clusterId == Guid.Empty)
            return;

        await CloseAsync();
        Navigation.NavigateTo($"/analytics/clusters/{clusterId}");
    }
}