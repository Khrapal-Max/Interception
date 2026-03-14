//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;

public sealed record AnalyticsClusterContextPageDto(
    Guid ClusterId,
    string Code,
    string? DisplayName,
    int DirectObservationsCount,
    int IndirectObservationsCount,
    IReadOnlyList<AnalyticsClusterContextObservationDto> DirectObservations,
    IReadOnlyList<AnalyticsClusterContextObservationDto> IndirectObservations,
    IReadOnlyList<AnalyticsClusterContextNodeDto> RelatedNodes,
    IReadOnlyList<AnalyticsClusterActionChainDto> ActionChains);