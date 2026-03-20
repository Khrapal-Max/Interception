//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Dtos;

namespace Interception.UI.Application.Reports.Abstractions;

public interface IDayPictureReportService
{
    Task<DayPictureReportDto> BuildAsync(DayPictureFilterDto filter, CancellationToken ct);
}
