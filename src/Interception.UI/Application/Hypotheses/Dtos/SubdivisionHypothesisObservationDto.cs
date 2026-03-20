//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Observation linked to subdivision hypothesis cluster.
/// </summary>
public sealed record SubdivisionHypothesisObservationDto(
    Guid LinkId,
    Guid ObservationId,
    DateTime ObservedDate,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? SubdivisionRaw,
    string? ObservationNote,
    string? LinkNote);
