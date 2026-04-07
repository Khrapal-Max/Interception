//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Будує операторську ієрархію груп поверх карти зв'язків.
/// </summary>
public interface IGroupHierarchyService
{
    /// <summary>
    /// Формує кластери груп за вибраний період.
    /// </summary>
    Task<GroupHierarchyMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
