//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationEditorModel
{
    public DateTime ObservedDate { get; set; } = DateTime.Now;
    public Guid? ObservationActionId { get; set; }
    public string? ObservationActionName { get; set; }
    public string ActionRaw { get; set; } = string.Empty;
    public string? Layer { get; set; }
    public string? RmRaw { get; set; }
    public string? PointRaw { get; set; }
    public string? LocationRaw { get; set; }
    public string? DistrictRaw { get; set; }
    public string? SubdivisionRaw { get; set; }
    public SubdivisionLinkStrength? SubdivisionStrength { get; set; }
    public ObservationSubdivisionSource SubdivisionSource { get; set; } = ObservationSubdivisionSource.Manual;
    public string? Note { get; set; }
    public List<ObservationParticipantEditorRow> Participants { get; set; } = [];
    public List<ObservationTagEditorRow> Tags { get; set; } = [];
    public List<ObservationProbableActionEditorRow> ProbableActions { get; set; } = [];
}
