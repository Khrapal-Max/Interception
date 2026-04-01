//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Interceptions.Dtos;

public sealed class InterceptionFilterDto
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Frequency { get; set; }
    public string? VectorSignal { get; set; }
    public string? ParticipantName { get; set; }
    public string? LabelName { get; set; }
}
