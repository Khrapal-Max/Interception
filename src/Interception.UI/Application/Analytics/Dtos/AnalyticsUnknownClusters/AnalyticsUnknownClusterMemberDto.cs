//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

/// <summary>
/// Учасник припущення.
/// </summary>
public sealed record AnalyticsUnknownClusterMemberDto(
    Guid ParticipantId,
    Guid ObservationId,
    int Ordinal,
    string DisplayName,
    bool IsUnknown,
    string? RoleRaw,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? LocationRaw);