//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Ієрархічний перехід між батьківською та дочірньою групами.
/// </summary>
public sealed record GroupHierarchyTransitionDto(
    string ParentGroupKey,
    string ParentCenterName,
    string ChildGroupKey,
    string ChildCenterName,
    string TransitionMemberName,
    string? TransitionMemberRole,
    bool HasDirectBridge,
    int Score);
