//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

/// <summary>
/// Повний DTO сторінки деталей unknown-кластера.
/// </summary>
public sealed record AnalyticsUnknownClusterDetailsPageDto(
    Guid ClusterId,
    string Code,
    string? DisplayName,
    string? Role,
    string Status,
    string? ArchiveReason,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    string? ConfirmedRole,
    int MembersCount,
    DateOnly? LastSeenDate,
    IReadOnlyList<AnalyticsUnknownClusterMemberDto> Members,
    IReadOnlyList<AnalyticsUnknownClusterObservationDto> ObservationHistory,
    IReadOnlyList<AnalyticsUnknownClusterRelatedPersonDto> RelatedPersons);