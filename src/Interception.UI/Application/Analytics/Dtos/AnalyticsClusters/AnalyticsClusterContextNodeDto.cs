//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;

public sealed record AnalyticsClusterContextNodeDto(
    string DisplayName,
    string NodeType,
    int SeenCount,
    DateOnly? LastSeenDate,
    IReadOnlyList<string> TopActions,
    IReadOnlyList<string> StrongSignals,
    int PriorityScore);