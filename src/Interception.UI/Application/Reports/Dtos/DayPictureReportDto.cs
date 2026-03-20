//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record DayPictureReportDto(
    DateOnly Day,
    DateTime GeneratedAtUtc,
    int TotalObservations,
    int TotalThreads,
    IReadOnlyList<DayPictureThreadDto> Threads);
