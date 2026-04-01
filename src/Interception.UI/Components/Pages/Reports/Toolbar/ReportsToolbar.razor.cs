//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Reports.Toolbar;

/// <summary>
/// Спільний toolbar для сторінок напряму «Звіти».
/// </summary>
public partial class ReportsToolbar : ComponentBase
{
    /// <summary>
    /// Ключ активної секції toolbar.
    /// </summary>
    [Parameter] public string ActiveSection { get; set; } = string.Empty;

    /// <summary>
    /// Додатковий мета-блок по центру toolbar.
    /// </summary>
    [Parameter] public RenderFragment? MetaContent { get; set; }

    /// <summary>
    /// Блок дій праворуч у toolbar.
    /// </summary>
    [Parameter] public RenderFragment? ActionsContent { get; set; }
}
