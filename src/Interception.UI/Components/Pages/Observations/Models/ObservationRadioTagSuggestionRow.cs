using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

/// <summary>
/// Suggested tag extracted from pasted service radio message.
/// </summary>
public sealed class ObservationRadioTagSuggestionRow
{
    public string Value { get; set; } = string.Empty;
    public TagKind Kind { get; set; } = TagKind.Keyword;
    public string? Reason { get; set; }
    public bool Applied { get; set; }
}
