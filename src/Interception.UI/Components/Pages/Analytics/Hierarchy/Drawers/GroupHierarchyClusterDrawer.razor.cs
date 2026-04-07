//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.Hierarchy.Drawers;

/// <summary>
/// Drawer деталей кластера груп.
/// </summary>
public partial class GroupHierarchyClusterDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public GroupHierarchyClusterDto? Cluster { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
}
