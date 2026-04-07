//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Зв'язок підпорядкування між двома групами.
/// </summary>
public sealed record GroupHierarchyEdgeDto(
    string ParentGroupKey,
    string ChildGroupKey,
    string ViaMemberName,
    string? ViaMemberRole,
    bool IsAmbiguous,
    int Depth);
