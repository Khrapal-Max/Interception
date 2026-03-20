//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record DayPictureObservationDto(
    Guid Id,
    DateTime ObservedDate,
    string ActionRaw,
    Guid? ObservationActionId,
    string? ObservationActionName,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? EffectiveSubdivision,
    bool SubdivisionResolved,
    IReadOnlyList<string> People,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ProbableActions,
    string? Note);
