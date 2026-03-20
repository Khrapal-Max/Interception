namespace Interception.UI.Components.Pages.Observations.Models;

/// <summary>
/// Parse result for service radio message block.
/// </summary>
public sealed class ObservationRadioParseResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SourcePost { get; set; }
    public string? SuggestedActionRaw { get; set; }
    public ObservationEditorModel Draft { get; set; } = new();
    public List<ObservationRadioTagSuggestionRow> SuggestedTags { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
