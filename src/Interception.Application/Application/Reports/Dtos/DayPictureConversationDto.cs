//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

/// <summary>
/// Хронологічний епізод / бесіда всередині підрозділу.
/// </summary>
public sealed record DayPictureConversationDto(
    string ConversationKey,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    int MessageCount,
    IReadOnlyList<DayPictureEntryDto> Entries);
