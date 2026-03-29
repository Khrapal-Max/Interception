//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Models.Candidates;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.LinkMap.Drawers;

/// <summary>
/// Дравер деталей окремої групи карти зв'язків.
/// </summary>
public partial class LinkMapGroupDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public LinkMapGroupModel? Group { get; set; }

    private string DrawerTitle => Group is null
        ? "Група карти зв'язків"
        : $"Група · {Group.KeyPersonName}";

    private int InternalConnectionWeight => Group is null
        ? 0
        : TryReadInt(Group, "InternalConnectionWeight") ?? 0;

    private int BridgeWeight => Group is null
        ? 0
        : TryReadInt(Group, "BridgeWeight") ?? Group.Bridges.Sum(x => x.Weight);

    private void OnDrawerClosed()
    {
        // Стан сторінки контролює parent, тут лише hook під shared Drawer.
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);

    private static int? TryReadInt(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var value = property.GetValue(source);
        return value is int intValue ? intValue : null;
    }
}
