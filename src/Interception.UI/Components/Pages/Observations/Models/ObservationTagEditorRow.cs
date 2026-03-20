//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationTagEditorRow
{
    public Guid? Id { get; set; }
    public string? RawValue { get; set; }
    public TagKind Kind { get; set; } = TagKind.Keyword;
    public ObservationTagSource Source { get; set; } = ObservationTagSource.Manual;
}
