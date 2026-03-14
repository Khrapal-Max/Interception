//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

sealed record ObservationRowDto(
        Guid Id,
        DateOnly ObservedDate,
        short DayPart,
        string ActionRaw,
        string ActionNorm,
        string? Layer,
        string? RmRaw,
        string? LocationRaw,
        string? DistrictRaw,
        string? Note);