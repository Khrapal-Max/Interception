//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Page DTO for unknown cluster registry.
/// </summary>
public sealed record AnalyticsUnknownClusterRegistryPageDto(
    int Total,
    IReadOnlyList<AnalyticsUnknownClusterRegistryItemDto> Items);