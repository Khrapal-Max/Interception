//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Analytics.Toolbar;

/// <summary>
/// Спільний toolbar для аналітичного блоку.
/// </summary>
public partial class CandidatesToolbar : ComponentBase
{
    /// <summary>
    /// Активна секція toolbar.
    /// </summary>
    [Parameter] public string ActiveSection { get; set; } = string.Empty;

    /// <summary>
    /// Ліва/центральна мета-інформація сторінки.
    /// </summary>
    [Parameter] public RenderFragment? MetaContent { get; set; }

    /// <summary>
    /// Правий блок дій сторінки.
    /// </summary>
    [Parameter] public RenderFragment? ActionsContent { get; set; }
}
