//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Коренева модель ієрархії груп для операторського режиму.
/// </summary>
public sealed record GroupHierarchyMapDto(
    IReadOnlyList<GroupHierarchyClusterDto> Clusters,
    int RootGroupCount,
    int ChildGroupCount,
    int CrossGroupNodeCount,
    int NeedsReviewCount);
