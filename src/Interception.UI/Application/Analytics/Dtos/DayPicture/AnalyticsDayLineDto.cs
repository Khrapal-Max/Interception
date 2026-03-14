//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.DayPicture;

public sealed record AnalyticsDayLineDto(
    string LineKey,
    string Title,
    IReadOnlyList<string> Signals,
    IReadOnlyList<AnalyticsDayObservationCardDto> Observations);
