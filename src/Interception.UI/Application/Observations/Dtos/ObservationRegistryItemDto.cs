//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed record ObservationRegistryItemDto(
    Guid Id,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? CompanyRaw,
    string? RmRaw,
    decimal? Layer,
    int ParticipantsCount,
    IReadOnlyList<ObservationParticipantDto> Participants);
