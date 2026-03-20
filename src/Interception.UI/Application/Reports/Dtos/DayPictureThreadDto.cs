//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record DayPictureThreadDto(
    int No,
    string Headline,
    IReadOnlyList<DayPictureObservationDto> Observations,
    IReadOnlyList<DayPictureLinkDto> Links,
    IReadOnlyList<DayPictureSignalDto> SharedSignals);
