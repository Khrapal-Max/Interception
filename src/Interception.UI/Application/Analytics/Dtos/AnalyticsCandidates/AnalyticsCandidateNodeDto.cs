//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateNodeDto(
    string DisplayName,
    string NodeType,
    int SeenCount,
    DateOnly? LastSeenDate,
    IReadOnlyList<string> TopActions,
    IReadOnlyList<string> StrongSignals);