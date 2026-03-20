//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Page of typed action catalog items.
/// </summary>
public sealed record ObservationActionCatalogPageDto(
    IReadOnlyList<ObservationActionCatalogItemDto> Items,
    int TotalCount);
