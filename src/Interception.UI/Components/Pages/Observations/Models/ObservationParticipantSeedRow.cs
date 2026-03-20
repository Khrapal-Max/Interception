namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationParticipantSeedRow
{
    public string? LabelRaw { get; set; }
    public string? RoleRaw { get; set; }
    public bool IsUnknown { get; set; }
    public int Ordinal { get; set; } = 1;
}
