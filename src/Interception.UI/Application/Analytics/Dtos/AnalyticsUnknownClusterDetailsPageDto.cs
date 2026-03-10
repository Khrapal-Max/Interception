//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Повний DTO сторінки деталей unknown-кластера.
/// </summary>
public sealed record AnalyticsUnknownClusterDetailsPageDto(
    Guid ClusterId,
    string Code,
    string? DisplayName,
    string Status,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    int MembersCount,
    DateOnly? LastSeenDate,
    IReadOnlyList<AnalyticsUnknownClusterMemberDto> Members,
    IReadOnlyList<AnalyticsUnknownClusterObservationDto> ObservationHistory,
    IReadOnlyList<AnalyticsUnknownClusterRelatedPersonDto> RelatedPersons);
