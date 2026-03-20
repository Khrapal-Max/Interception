//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Page of confirmed subdivision catalog items.
/// </summary>
public sealed record ResolvedSubdivisionCatalogPageDto(
    IReadOnlyList<ResolvedSubdivisionCatalogItemDto> Items,
    int TotalCount);
