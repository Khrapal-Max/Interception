//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Денна картина повідомлень.
/// </summary>
public sealed record DayPictureModel(
    DateOnly Day,
    int TotalMessages,
    IReadOnlyList<DayPictureGroupModel> Groups);
