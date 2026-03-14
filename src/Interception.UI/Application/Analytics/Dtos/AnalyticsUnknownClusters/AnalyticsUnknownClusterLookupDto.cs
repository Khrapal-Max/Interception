//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

/// <summary>
/// Короткий DTO для вибору existing unknown-кластера.
/// </summary>
public sealed record AnalyticsUnknownClusterLookupDto(
    Guid Id,
    string Code,
    string? DisplayName,
    string Status,
    int MembersCount,
    string? ResolvedActorDisplayName);
