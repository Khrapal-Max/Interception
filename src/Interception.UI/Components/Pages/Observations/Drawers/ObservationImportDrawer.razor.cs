//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationImportDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnImported { get; set; }

    private async Task OnImportedInternal()
    {
        if (OnImported.HasDelegate) await OnImported.InvokeAsync();
        if (OnClose.HasDelegate) await OnClose.InvokeAsync();
    }
}
