//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for resolving a subdivision hypothesis into a confirmed subdivision.
/// </summary>
public sealed record ResolvedSubdivisionUpsertDto(
    string Name,
    string? LayerHint,
    string? RmHint,
    string? Note);
