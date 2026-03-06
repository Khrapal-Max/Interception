//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed record ObservationRegistryItemDto(
    Guid Id,
    DateOnly ObservedDate,
    short DayPart,
    string? Layer,
    string? RmRaw,
    string? PointRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string ActionRaw,
    string? Note,
    int ParticipantsCount,
    IReadOnlyList<ObservationParticipantDto> Participants);
