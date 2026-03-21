//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Enums;

/// <summary>
/// Статус групи кандидатів на ідентифікацію.
/// </summary>
public enum CandidateGroupStatus
{
    /// <summary>Щойно створена, очікує рішення оператора.</summary>
    Open = 0,

    /// <summary>Оператор підтвердив — учасники ідентифіковані.</summary>
    Confirmed = 1,

    /// <summary>Оператор відхилив — учасники залишаються невідомими.</summary>
    Dismissed = 2
}
