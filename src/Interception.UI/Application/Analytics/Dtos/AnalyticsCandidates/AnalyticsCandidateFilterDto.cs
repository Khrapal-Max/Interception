//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateFilterDto(
    string? Query,
    string? CandidateType,
    bool OnlyWithoutHypothesis,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int Skip,
    int Take);
