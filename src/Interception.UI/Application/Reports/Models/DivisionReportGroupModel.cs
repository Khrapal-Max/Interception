//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Блок звіту по одному підрозділу.
/// </summary>
public sealed record DivisionReportGroupModel(
    string Division,
    IReadOnlyList<string> Frequencies,
    int UnknownMentionsCount,
    int UnknownGroupsCount,
    IReadOnlyList<DivisionReportPersonRowModel> People);
