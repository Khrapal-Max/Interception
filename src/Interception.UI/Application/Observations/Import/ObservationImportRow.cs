//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Import;

public sealed record ObservationImportRow(
    DateOnly ObservedDate,
    short DayPart,
    string ActionRaw,
    decimal? Layer,
    string? RmRaw,
    string? PointRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? CompanyRaw,
    string? Note,
    IReadOnlyList<ObservationImportParticipant> Participants);
