//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Create/update payload for reusable tag catalog item.
/// </summary>
public sealed record TagCatalogUpsertDto(
    string Name,
    TagKind Kind);
