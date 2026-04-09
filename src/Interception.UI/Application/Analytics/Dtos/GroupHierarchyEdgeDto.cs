//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Зв'язок підпорядкування між двома групами.
/// Якщо ребро підтверджене explicit контуром керування між особами — <see cref="IsDirective"/> дорівнює true.
/// </summary>
public sealed record GroupHierarchyEdgeDto(
    string ParentGroupKey,
    string ChildGroupKey,
    string ViaMemberName,
    string? ViaMemberRole,
    bool IsAmbiguous,
    int Depth,
    bool IsDirective,
    string? DirectiveLabel);
