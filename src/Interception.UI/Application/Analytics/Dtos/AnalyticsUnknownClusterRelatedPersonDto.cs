//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Узагальнений DTO пов'язаної особи або вузла, який зустрічався поруч із кластером.
/// </summary>
public sealed record AnalyticsUnknownClusterRelatedPersonDto(
    string DisplayName,
    string Kind,
    int SeenCount,
    DateOnly? LastSeenDate);
