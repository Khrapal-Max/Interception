//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Full reusable tag catalog item for editor/details form.
/// </summary>
public sealed record TagCatalogDetailsDto(
    Guid Id,
    string Name,
    TagKind Kind,
    bool IsActive,
    int UsageCount,
    DateTime CreatedAtUtc,
    string? CreatedBy);
