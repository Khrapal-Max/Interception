//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;

public sealed record AnalyticsClusterActionChainDto(
    string ActionRaw,
    int SeenCount,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Signals);