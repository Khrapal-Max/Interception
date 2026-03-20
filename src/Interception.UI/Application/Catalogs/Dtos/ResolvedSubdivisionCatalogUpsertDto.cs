//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Create/update payload for confirmed subdivision catalog item.
/// </summary>
public sealed record ResolvedSubdivisionCatalogUpsertDto(
    string Name,
    string? LayerHint,
    string? RmHint,
    string? Note);
