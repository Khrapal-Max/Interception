//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

public interface IObservationWriteService
{
    Task<ObservationCreateResultDto> CreateAsync(ObservationCreateRequestDto request, CancellationToken ct);
}
