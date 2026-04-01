//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Reports.Dtos;

/// <summary>
/// Рядок відомої особи в межах конкретного підрозділу.
/// </summary>
public sealed record DivisionReportPersonRowDto(
    string PersonKey,
    string Name,
    string? Role,
    DateTime LastSeenAt);
