//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Dtos;

namespace Interception.UI.Application.Catalogs.Abstractions;

/// <summary>
/// Application service for reusable tag catalog.
/// </summary>
public interface ITagCatalogService
{
    Task<TagCatalogPageDto> SearchAsync(TagCatalogFilterDto filter, CancellationToken ct);

    Task<TagCatalogDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<CatalogSaveResultDto> CreateAsync(TagCatalogUpsertDto request, string? createdBy, CancellationToken ct);

    Task<CatalogSaveResultDto> UpdateAsync(Guid id, TagCatalogUpsertDto request, CancellationToken ct);

    Task ArchiveAsync(Guid id, CancellationToken ct);

    Task RestoreAsync(Guid id, CancellationToken ct);
}
