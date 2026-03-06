//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

public interface IObservationRegistryService
{
    Task<ObservationRegistryPageDto> SearchAsync(ObservationRegistryFilter filter, CancellationToken ct);
    Task<ObservationDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);
}
