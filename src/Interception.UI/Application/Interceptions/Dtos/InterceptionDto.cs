//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Application.Interceptions.Dtos;

public sealed class InterceptionFormDto
{
    // DateTimeConverter.Now = DateTime.UtcNow — Kind=Utc, Npgsql приймає
    public DateTime ObservedDate { get; set; } = ConverterDateTimeExtensions.Now;
    public string? Frequency { get; set; }
    public string? Division { get; set; }
    public string? PointSignal { get; set; }
    public string? VectorSignal { get; set; }
    public string? LocationDetails { get; set; }
    public LocationClass LocationClass { get; set; } = LocationClass.NoInfo;
    public Guid InterceptionActionId { get; set; }
    public string? Note { get; set; }

    public List<ParticipantFormDto> Participants { get; set; } = [];
    public List<string> Labels { get; set; } = [];
}
