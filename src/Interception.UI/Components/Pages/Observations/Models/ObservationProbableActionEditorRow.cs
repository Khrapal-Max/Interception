//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationProbableActionEditorRow
{
    public Guid? Id { get; set; }
    public Guid ObservationActionId { get; set; }
    public string ObservationActionName { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 0.50m;
    public string? Reason { get; set; }
    public ProbableActionSource Source { get; set; } = ProbableActionSource.Manual;
}
