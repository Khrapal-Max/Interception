//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationParticipantEditorRow
{
    public Guid? Id { get; set; }
    public string? LabelRaw { get; set; }
    public bool IsUnknown { get; set; }
    public string? RoleRaw { get; set; }
    public int Ordinal { get; set; } = 1;
}
