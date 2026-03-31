//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Блок підрозділу в картині дня.
/// </summary>
public sealed record DayPictureGroupModel(
    string GroupKey,
    string? Division,
    int MessageCount,
    IReadOnlyList<DayPictureConversationModel> Conversations);
