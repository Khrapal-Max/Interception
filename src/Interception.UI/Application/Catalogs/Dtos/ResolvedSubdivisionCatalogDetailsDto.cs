//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Full confirmed subdivision catalog item for editor/details form.
/// </summary>
public sealed record ResolvedSubdivisionCatalogDetailsDto(
    Guid Id,
    string Name,
    string? LayerHint,
    string? RmHint,
    string? Note,
    bool IsActive,
    int LinkedClustersCount,
    DateTime CreatedAtUtc,
    string? CreatedBy);
