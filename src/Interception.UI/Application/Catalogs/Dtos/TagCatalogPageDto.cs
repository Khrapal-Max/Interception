//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Page of reusable tag catalog items.
/// </summary>
public sealed record TagCatalogPageDto(
    IReadOnlyList<TagCatalogItemDto> Items,
    int TotalCount);
