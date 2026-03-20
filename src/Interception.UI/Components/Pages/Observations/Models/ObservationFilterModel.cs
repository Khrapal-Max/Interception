//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

/// <summary>
/// UI-модель фільтра реєстру спостережень.
/// </summary>
public sealed class ObservationFilterModel
{
    /// <summary>
    /// Вільний текстовий пошук по спостереженнях.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Нижня межа дати та часу спостереження.
    /// </summary>
    public DateTime? ObservedFrom { get; set; }

    /// <summary>
    /// Верхня межа дати та часу спостереження.
    /// </summary>
    public DateTime? ObservedTo { get; set; }

    /// <summary>
    /// Сила зв'язку підрозділу.
    /// </summary>
    public SubdivisionLinkStrength? SubdivisionStrength { get; set; }

    /// <summary>
    /// Ознака фільтрації тільки спостережень з невідомими учасниками.
    /// </summary>
    public bool OnlyUnknownParticipants { get; set; }

    /// <summary>
    /// Ознака фільтрації тільки спостережень з типізованою дією.
    /// </summary>
    public bool OnlyBoundAction { get; set; }
}
