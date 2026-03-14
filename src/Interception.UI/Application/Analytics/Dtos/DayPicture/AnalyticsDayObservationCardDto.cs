//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.DayPicture;

public sealed record AnalyticsDayObservationCardDto(
    Guid ObservationId,
    short DayPart,
    string ActionRaw,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? Note,
    IReadOnlyList<string> Participants);
