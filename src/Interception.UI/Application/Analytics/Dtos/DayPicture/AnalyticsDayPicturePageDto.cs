//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.DayPicture;    

public sealed record AnalyticsDayPicturePageDto(
      DateOnly Date,
      IReadOnlyList<AnalyticsDayLineDto> Lines,
      IReadOnlyList<AnalyticsDayCandidateDto> TopCandidates);
