//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Filter for typed action catalog registry.
/// </summary>
public sealed record ObservationActionCatalogFilterDto(
    string? Query,
    ObservationActionCategory? Category,
    bool IncludeArchived,
    int Skip,
    int Take);
