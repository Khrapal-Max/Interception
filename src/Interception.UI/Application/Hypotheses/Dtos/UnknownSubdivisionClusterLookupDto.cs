//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Lightweight lookup item for open subdivision hypotheses.
/// </summary>
public sealed record UnknownSubdivisionClusterLookupDto(
    Guid Id,
    string LabelRaw,
    string? LayerHint,
    string? RmHint,
    int ObservationsCount,
    bool IsResolved,
    bool IsArchived,
    string? ResolvedSubdivisionName);
