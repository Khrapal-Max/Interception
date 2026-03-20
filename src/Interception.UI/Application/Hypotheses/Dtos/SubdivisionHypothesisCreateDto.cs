//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for creating a new subdivision hypothesis cluster.
/// </summary>
public sealed record SubdivisionHypothesisCreateDto(
    string LabelRaw,
    string? LayerHint,
    string? RmHint,
    string? Note,
    IReadOnlyList<Guid> SeedObservationIds);
