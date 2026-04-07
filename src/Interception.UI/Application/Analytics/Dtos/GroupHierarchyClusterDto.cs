//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Кластер груп із кореневою опорною групою і дочірніми контурами.
/// </summary>
public sealed record GroupHierarchyClusterDto(
    string ClusterKey,
    string RootGroupKey,
    string RootCenterName,
    string? RootCenterRole,
    string? Division,
    IReadOnlyList<string> Frequencies,
    string? PrimaryAction,
    IReadOnlyList<string> TopActions,
    int TotalGroups,
    int TotalUniqueMembers,
    int TransitionCount,
    int DirectBridgeCount,
    IReadOnlyList<GroupHierarchyNodeDto> Nodes,
    IReadOnlyList<GroupHierarchyTransitionDto> Transitions);
