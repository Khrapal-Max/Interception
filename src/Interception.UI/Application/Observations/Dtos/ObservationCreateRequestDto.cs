//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed record ObservationCreateRequestDto(
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? PointRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? Note,
    IReadOnlyList<ObservationCreateParticipantDto> Participants);
