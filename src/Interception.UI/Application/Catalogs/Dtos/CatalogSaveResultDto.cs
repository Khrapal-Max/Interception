//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Generic save result for catalog entities.
/// </summary>
public sealed record CatalogSaveResultDto(
    Guid Id,
    bool Created);
