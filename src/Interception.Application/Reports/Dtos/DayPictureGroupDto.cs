//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Reports.Dtos;

/// <summary>
/// Блок підрозділу в картині дня.
/// </summary>
public sealed record DayPictureGroupDto(
    string GroupKey,
    string? Division,
    int MessageCount,
    IReadOnlyList<DayPictureConversationDto> Conversations);
