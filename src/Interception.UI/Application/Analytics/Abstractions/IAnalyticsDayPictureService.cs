//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos.DayPicture;

namespace Interception.UI.Application.Analytics.Abstractions;

public interface IAnalyticsDayPictureService
{
    Task<AnalyticsDayPicturePageDto> GetPageAsync(DateOnly date, CancellationToken ct);
}