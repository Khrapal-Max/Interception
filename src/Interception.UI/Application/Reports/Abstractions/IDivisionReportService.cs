//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Models;

namespace Interception.UI.Application.Reports.Abstractions;

/// <summary>
/// Будує зведений звіт по підрозділах.
/// Для кожного підрозділу повертає список частот,
/// метрики по НВ і таблицю відомих людей.
/// </summary>
public interface IDivisionReportService
{
    /// <summary>
    /// Формує зведений звіт у вибраному діапазоні дат.
    /// </summary>
    Task<DivisionReportModel> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
