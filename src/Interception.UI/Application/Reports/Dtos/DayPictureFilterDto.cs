//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record DayPictureFilterDto(
    DateOnly Day,
    string? Layer = null,
    string? RmRaw = null,
    string? Query = null,
    int MaxObservations = 300,
    short MinLinkScore = 2);
