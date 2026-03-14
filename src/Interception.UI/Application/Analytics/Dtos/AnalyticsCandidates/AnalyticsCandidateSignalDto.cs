//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateSignalDto(
    string Name,
    int Weight,
    string Explanation);