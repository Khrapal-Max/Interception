//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Dtos;

namespace Interception.UI.Application.Catalogs.Abstractions;

/// <summary>
/// Application service for typed action catalog.
/// </summary>
public interface IObservationActionCatalogService
{
    Task<ObservationActionCatalogPageDto> SearchAsync(ObservationActionCatalogFilterDto filter, CancellationToken ct);

    Task<ObservationActionCatalogDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<CatalogSaveResultDto> CreateAsync(ObservationActionCatalogUpsertDto request, string? createdBy, CancellationToken ct);

    Task<CatalogSaveResultDto> UpdateAsync(Guid id, ObservationActionCatalogUpsertDto request, CancellationToken ct);

    Task ArchiveAsync(Guid id, CancellationToken ct);

    Task RestoreAsync(Guid id, CancellationToken ct);
}
