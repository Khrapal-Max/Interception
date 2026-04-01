//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Reports.Dtos;

/// <summary>
/// Кореневий DTO зведеного звіту по підрозділах.
/// </summary>
public sealed record DivisionReportDto(
    IReadOnlyList<DivisionReportGroupDto> Groups);
