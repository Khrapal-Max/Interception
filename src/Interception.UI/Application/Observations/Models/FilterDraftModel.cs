//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Models;

public sealed class FilterDraftModel
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public short? DayPart { get; set; }
    public string? Person { get; set; }
    public string? Action { get; set; }
    public string? Location { get; set; }
    public string? District { get; set; }
    public string? Company { get; set; }
    public string? Rm { get; set; }
    public int Take { get; set; } = 50;
}
