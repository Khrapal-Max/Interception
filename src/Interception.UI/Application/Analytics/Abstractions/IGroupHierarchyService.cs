//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Будує read-side ієрархії груп поверх уже порахованої карти зв'язків.
/// Не змінює домен і не підміняє прямі мости між групами.
/// </summary>
public interface IGroupHierarchyService
{
    /// <summary>
    /// Повертає кластери груп з ієрархічними зв'язками за вибраний період.
    /// </summary>
    Task<GroupHierarchyDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
