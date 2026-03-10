//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Короткий DTO спостереження, у якому фігурував кластер.
/// </summary>
public sealed record AnalyticsUnknownClusterObservationDto(
    Guid ObservationId,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    int ParticipantsCount);
