//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Reports;

/// <summary>
/// Кореневий DTO зведеного звіту по підрозділах.
/// </summary>
public sealed record DivisionReportModel(
    IReadOnlyList<DivisionReportGroupModel> Groups);
