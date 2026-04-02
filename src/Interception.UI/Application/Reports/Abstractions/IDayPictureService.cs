//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Dtos;

namespace Interception.UI.Application.Reports.Abstractions;

/// <summary>
/// Будує денну картину пов'язаних спостережень.
/// </summary>
public interface IDayPictureService
{
    /// <summary>
    /// Повертає денну картину спостережень за вказану дату.
    /// </summary>
    Task<DayPictureDto> BuildAsync(DateTime day, CancellationToken ct = default);
}
