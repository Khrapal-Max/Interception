//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Registry.Toolbar;

/// <summary>
/// Спільний toolbar для сторінок напряму «Регістри».
/// </summary>
public partial class RegistriesToolbar : ComponentBase
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
