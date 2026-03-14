//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс реєстру аналітичних кластерів невизначених осіб.
/// Працює окремо від операторського контуру та не змінює raw-спостереження.
/// </summary>
public interface IAnalyticsUnknownClusterRegistryService
{
    /// <summary>
    /// Повертає сторінку реєстру кластерів з пошуком, фільтром статусу та пагінацією.
    /// </summary>
    Task<AnalyticsUnknownClusterRegistryPageDto> SearchAsync(AnalyticsUnknownClusterRegistryFilter filter, CancellationToken ct);
}
