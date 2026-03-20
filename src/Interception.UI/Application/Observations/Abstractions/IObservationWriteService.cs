//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

/// <summary>
/// Write-side service for creating and editing observations.
/// </summary>
public interface IObservationWriteService
{
    /// <summary>
    /// Creates a new observation.
    /// </summary>
    Task<ObservationSaveResultDto> CreateAsync(ObservationUpsertRequestDto request, CancellationToken ct);

    /// <summary>
    /// Updates an existing observation.
    /// </summary>
    Task<ObservationSaveResultDto> UpdateAsync(Guid observationId, ObservationUpsertRequestDto request, CancellationToken ct);
}
