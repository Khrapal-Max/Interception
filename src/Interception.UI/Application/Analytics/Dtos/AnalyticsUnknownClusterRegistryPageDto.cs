//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Сторінка реєстру аналітичних кластерів.
/// </summary>
public sealed record AnalyticsUnknownClusterRegistryPageDto(
    IReadOnlyList<AnalyticsUnknownClusterRegistryItemDto> Items,
    int Total);
