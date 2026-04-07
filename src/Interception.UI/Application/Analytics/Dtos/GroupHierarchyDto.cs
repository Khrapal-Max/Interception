//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Коренева модель сторінки ієрархії груп.
/// </summary>
public sealed record GroupHierarchyDto(
    IReadOnlyList<GroupHierarchyClusterDto> Clusters);
