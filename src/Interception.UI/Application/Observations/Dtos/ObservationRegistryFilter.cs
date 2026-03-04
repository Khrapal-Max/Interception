//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed class ObservationRegistryFilter
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public short? DayPart { get; init; } // 1..2 (keep DTO-friendly)

    public string? Person { get; init; }     // callsign/name, "НВ" supported
    public string? Action { get; init; }
    public string? Location { get; init; }
    public string? District { get; init; }
    public string? Company { get; init; }
    public string? Rm { get; init; }

    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 50;
}
