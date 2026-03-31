//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Models;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.LinkMap.Drawers;

/// <summary>
/// Drawer деталей групи карти зв'язків.
/// Показує профіль групи та її склад без дублювання списку зв'язків зі сторінки.
/// </summary>
public partial class LinkMapGroupDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public LinkMapGroupModel? Group { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected async Task CloseAsync()
    {
        if (OnClose.HasDelegate)
            await OnClose.InvokeAsync();
    }
}
