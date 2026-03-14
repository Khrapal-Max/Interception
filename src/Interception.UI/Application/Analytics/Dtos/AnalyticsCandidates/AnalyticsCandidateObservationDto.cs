//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateObservationDto(
    Guid ObservationId,
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? Note,
    bool DirectMatch,
    int MatchScore,
    IReadOnlyList<string> MatchedSignals);
