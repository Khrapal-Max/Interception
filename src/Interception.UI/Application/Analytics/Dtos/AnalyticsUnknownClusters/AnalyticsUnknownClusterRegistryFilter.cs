//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

/// <summary>
/// Фільтр реєстру аналітичних кластерів.
/// </summary>
public sealed class AnalyticsUnknownClusterRegistryFilter
{
    /// <summary>
    /// Пошук за кодом, назвою кластера або встановленою особою.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Фільтр за статусом кластера.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Зміщення для пагінації.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Кількість рядків на сторінку.
    /// </summary>
    public int Take { get; set; } = 20;
}
