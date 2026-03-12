//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed record ObservationDetailsDto(
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
    string Source,
    Guid? SourceFileId,
    int? SourceRow,
    string ContentHash,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    IReadOnlyList<ObservationParticipantDto> Participants);