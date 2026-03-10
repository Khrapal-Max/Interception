//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Повний DTO сторінки аналітичної резолюції raw-учасника.
/// </summary>
public sealed record AnalyticsParticipantResolutionPageDto(
    Guid ParticipantId,
    Guid ObservationId,
    int Ordinal,
    string RawDisplayName,
    bool IsUnknown,
    string? RoleRaw,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? Note,
    Guid? UnknownClusterId,
    string? UnknownClusterCode,
    string? UnknownClusterDisplayName,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    IReadOnlyList<AnalyticsRelatedParticipantDto> RelatedParticipants);
