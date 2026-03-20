//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Filter for confirmed subdivision catalog registry.
/// </summary>
public sealed record ResolvedSubdivisionCatalogFilterDto(
    string? Query,
    bool IncludeArchived,
    int Skip,
    int Take);
