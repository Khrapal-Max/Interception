//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Lightweight row for subdivision hypothesis registry.
/// </summary>
public sealed record SubdivisionHypothesisRegistryItemDto(
    Guid Id,
    string LabelRaw,
    string? LayerHint,
    string? RmHint,
    string? Note,
    int ObservationsCount,
    Guid? ResolvedSubdivisionId,
    string? ResolvedSubdivisionName,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    DateTime? LastSeenAtUtc);
