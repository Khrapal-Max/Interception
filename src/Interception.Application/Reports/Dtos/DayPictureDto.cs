//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Reports.Dtos;

/// <summary>
/// Денна картина повідомлень.
/// </summary>
public sealed record DayPictureDto(
    DateOnly Day,
    int TotalMessages,
    IReadOnlyList<DayPictureGroupDto> Groups);
