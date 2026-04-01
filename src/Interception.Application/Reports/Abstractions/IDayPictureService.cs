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

using Interception.Application.Reports.Dtos;

namespace Interception.Application.Reports.Abstractions;

/// <summary>
/// Будує денну картину пов'язаних спостережень.
/// </summary>
public interface IDayPictureService
{
    /// <summary>
    /// Повертає денну картину спостережень за вказану дату.
    /// </summary>
    Task<DayPictureDto> BuildAsync(DateOnly day, CancellationToken ct = default);
}
