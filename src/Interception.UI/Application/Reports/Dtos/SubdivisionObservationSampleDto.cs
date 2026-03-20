//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionObservationSampleDto(
    Guid ObservationId,
    DateTime ObservedDate,
    string ActionRaw,
    string? ObservationActionName,
    string? LocationRaw,
    string? DistrictRaw,
    IReadOnlyList<string> People);
