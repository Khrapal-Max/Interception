//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Models;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationFilterDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public FilterDraftModel Draft { get; set; } = new();
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnApply { get; set; }
    [Parameter] public EventCallback OnClear { get; set; }

    private async Task Apply()
    {
        if (OnApply.HasDelegate) await OnApply.InvokeAsync();
        if (OnClose.HasDelegate) await OnClose.InvokeAsync();
    }

    private async Task Clear()
    {
        if (OnClear.HasDelegate) await OnClear.InvokeAsync();
    }
}
