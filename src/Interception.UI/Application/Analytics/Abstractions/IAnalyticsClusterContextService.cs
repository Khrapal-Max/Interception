//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;

namespace Interception.UI.Application.Analytics.Abstractions;

public interface IAnalyticsClusterContextService
{
    Task<AnalyticsClusterContextPageDto?> GetPageAsync(Guid clusterId, CancellationToken ct);
}
