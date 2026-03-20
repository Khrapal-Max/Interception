using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationTagSeedRow
{
    public string? RawValue { get; set; }
    public TagKind Kind { get; set; } = TagKind.Keyword;
    public ObservationTagSource Source { get; set; } = ObservationTagSource.Manual;
}
