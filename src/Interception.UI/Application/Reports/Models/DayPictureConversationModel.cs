//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Хронологічний епізод / бесіда всередині підрозділу.
/// </summary>
public sealed record DayPictureConversationModel(
    string ConversationKey,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    int MessageCount,
    IReadOnlyList<DayPictureEntryModel> Entries);
