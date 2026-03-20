//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionReportDto(
    DateTime GeneratedAtUtc,
    DateTime? ObservedFrom,
    DateTime? ObservedTo,
    int TotalObservations,
    int TotalGroups,
    IReadOnlyList<SubdivisionReportItemDto> Items);
