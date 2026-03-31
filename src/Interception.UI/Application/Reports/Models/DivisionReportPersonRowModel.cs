//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Рядок відомої особи в межах конкретного підрозділу.
/// </summary>
public sealed record DivisionReportPersonRowModel(
    string PersonKey,
    string Name,
    string? Role,
    DateTime LastSeenAt);
