//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Рядок змісту групи в операторській деталізації кластера.
/// </summary>
public sealed record GroupHierarchyActionRowDto(
    string GroupKey,
    string GroupTitle,
    string? PrimaryAction,
    string Signal,
    int Depth);
