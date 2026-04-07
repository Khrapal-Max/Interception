//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Вузол групової ієрархії.
/// </summary>
public sealed record GroupHierarchyNodeDto(
    string GroupKey,
    string Title,
    string? Division,
    string KeyPersonName,
    string? KeyPersonRole,
    IReadOnlyList<string> Frequencies,
    string? PrimaryAction,
    IReadOnlyList<string> TopActions,
    int MemberCount,
    int BridgeCount,
    int Depth,
    bool IsRoot,
    bool NeedsReview);
