//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Row in reusable tag catalog registry.
/// </summary>
public sealed record TagCatalogItemDto(
    Guid Id,
    string Name,
    TagKind Kind,
    bool IsActive,
    int UsageCount,
    DateTime CreatedAtUtc);
