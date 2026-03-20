//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Filter for reusable tag catalog registry.
/// </summary>
public sealed record TagCatalogFilterDto(
    string? Query,
    TagKind? Kind,
    bool IncludeArchived,
    int Skip,
    int Take);
