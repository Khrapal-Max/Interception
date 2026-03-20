//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Full analytical details for subdivision hypothesis cluster.
/// </summary>
public sealed record SubdivisionHypothesisDetailsDto(
    Guid Id,
    string LabelRaw,
    string? LayerHint,
    string? RmHint,
    string? Note,
    Guid? ResolvedSubdivisionId,
    string? ResolvedSubdivisionName,
    bool IsArchived,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<SubdivisionHypothesisObservationDto> Observations);
