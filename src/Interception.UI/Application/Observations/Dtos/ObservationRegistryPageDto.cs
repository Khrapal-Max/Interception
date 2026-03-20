//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Paged observation registry response.
/// </summary>
public sealed record ObservationRegistryPageDto(
    IReadOnlyList<ObservationRegistryItemDto> Items,
    int TotalCount);
