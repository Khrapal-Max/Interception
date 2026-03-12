//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Related persons / nodes around cluster from shared observations.
/// </summary>
public sealed record AnalyticsUnknownClusterRelatedPersonDto(
    string DisplayName,
    string Kind,
    int SeenCount,
    DateOnly? LastSeenDate);