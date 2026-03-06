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
    [Parameter] public Guid? ObservationId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private Guid? _loadedId;
    private ObservationDetailsDto? _dto;
    private bool _loading;
    private string? _error;

    protected override async Task OnParametersSetAsync()
    {
        if (ObservationId is null)
        {
            _loadedId = null;
            _dto = null;
            _error = null;
            _loading = false;
            return;
        }

        if (_loadedId == ObservationId)
            return;

        _loadedId = ObservationId;
        _dto = null;
        _error = null;
        _loading = true;

        try
        {
            _dto = await RegistryService.GetByIdAsync(ObservationId.Value, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task Close()
    {
        _loadedId = null;
        _dto = null;
        _error = null;
        _loading = false;

        if (OnClose.HasDelegate)
            await OnClose.InvokeAsync();
    }
}
