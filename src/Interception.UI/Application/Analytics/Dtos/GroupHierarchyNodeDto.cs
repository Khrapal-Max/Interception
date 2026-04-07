//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Вузол групи всередині ієрархічного кластера.
/// </summary>
public sealed record GroupHierarchyNodeDto(
    string GroupKey,
    int Level,
    string CenterName,
    string? CenterRole,
    string? Division,
    IReadOnlyList<string> Frequencies,
    string? PrimaryAction,
    IReadOnlyList<string> TopActions,
    int MemberCount,
    int BridgeCount,
    bool IsRoot,
    string? ParentGroupKey,
    string? ParentCenterName,
    string? TransitionMemberName,
    string? TransitionMemberRole,
    bool HasDirectBridgeToParent,
    int ChildCount);
