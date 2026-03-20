//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Distinct observation in person hypothesis history.
/// </summary>
public sealed record ActorHypothesisObservationDto(
    Guid ObservationId,
    DateTime ObservedDate,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? SubdivisionRaw,
    string? Note,
    int ParticipantsCount);
