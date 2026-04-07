//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Один кластер ієрархії: опорна група та підлеглі контури.
/// </summary>
public sealed record GroupHierarchyClusterDto(
    string ClusterKey,
    GroupHierarchyNodeDto Root,
    IReadOnlyList<GroupHierarchyNodeDto> Nodes,
    IReadOnlyList<GroupHierarchyEdgeDto> Edges,
    IReadOnlyList<GroupHierarchyActionRowDto> ActionRows,
    string Summary,
    bool NeedsReview);
