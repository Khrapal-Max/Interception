//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Row in confirmed subdivision catalog registry.
/// </summary>
public sealed record ResolvedSubdivisionCatalogItemDto(
    Guid Id,
    string Name,
    string? LayerHint,
    string? RmHint,
    bool IsActive,
    int LinkedClustersCount,
    DateTime CreatedAtUtc);
