//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

namespace Interception.UI.Application.Analytics.Abstractions;

public interface IAnalyticsCandidateService
{
    Task<AnalyticsCandidatePageDto> GetPageAsync(AnalyticsCandidateFilterDto filter, CancellationToken ct);

    Task<AnalyticsCandidateDetailsDto?> GetDetailsAsync(string candidateKey, CancellationToken ct);
}