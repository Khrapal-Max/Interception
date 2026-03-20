//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Dtos;

namespace Interception.UI.Application.Catalogs.Abstractions;

/// <summary>
/// Application service for confirmed subdivision catalog.
/// </summary>
public interface IResolvedSubdivisionCatalogService
{
    Task<ResolvedSubdivisionCatalogPageDto> SearchAsync(ResolvedSubdivisionCatalogFilterDto filter, CancellationToken ct);

    Task<ResolvedSubdivisionCatalogDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<CatalogSaveResultDto> CreateAsync(ResolvedSubdivisionCatalogUpsertDto request, string? createdBy, CancellationToken ct);

    Task<CatalogSaveResultDto> UpdateAsync(Guid id, ResolvedSubdivisionCatalogUpsertDto request, CancellationToken ct);

    Task ArchiveAsync(Guid id, CancellationToken ct);

    Task RestoreAsync(Guid id, CancellationToken ct);
}
