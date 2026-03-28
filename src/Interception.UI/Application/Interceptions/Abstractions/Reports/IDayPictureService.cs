//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Models.Reports;

namespace Interception.UI.Application.Interceptions.Abstractions.Reports;

/// <summary>
/// Будує денну картину пов'язаних спостережень.
/// </summary>
public interface IDayPictureService
{
    /// <summary>
    /// Повертає денну картину спостережень за вказану дату.
    /// </summary>
    Task<DayPictureModel> BuildAsync(DateOnly day, CancellationToken ct = default);
}
