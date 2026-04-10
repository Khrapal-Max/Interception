//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.Toolbar;

/// <summary>
/// Спільний toolbar для блоку «Спостереження».
/// </summary>
public partial class InterceptionsToolbar : ComponentBase
{
    [Parameter] public string ActiveSection { get; set; } = string.Empty;
    [Parameter] public RenderFragment? MetaContent { get; set; }
    [Parameter] public RenderFragment? ActionsContent { get; set; }
}
