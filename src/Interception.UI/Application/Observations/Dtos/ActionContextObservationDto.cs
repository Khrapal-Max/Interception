//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Схоже спостереження, яке допомагає оператору зрозуміти ланцюжок дії.
/// </summary>
public sealed record ActionContextObservationDto(
    Guid ObservationId,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? LocationRaw,
    string? DistrictRaw,
    IReadOnlyList<string> Participants,
    int Score);
