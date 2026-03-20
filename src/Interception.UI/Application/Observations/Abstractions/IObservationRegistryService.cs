//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

/// <summary>
/// Read-side service for observation registry and details.
/// </summary>
public interface IObservationRegistryService
{
    /// <summary>
    /// Searches observations with paging and lightweight preview data.
    /// </summary>
    Task<ObservationRegistryPageDto> SearchAsync(ObservationRegistryFilterDto filter, CancellationToken ct);

    /// <summary>
    /// Returns full observation details.
    /// </summary>
    Task<ObservationDetailsDto?> GetByIdAsync(Guid observationId, CancellationToken ct);
}
