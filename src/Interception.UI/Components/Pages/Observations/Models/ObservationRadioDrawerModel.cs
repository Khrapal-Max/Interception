namespace Interception.UI.Components.Pages.Observations.Models;

/// <summary>
/// UI state for service radio observation drawer.
/// </summary>
public sealed class ObservationRadioDrawerModel
{
    public string RawText { get; set; } = string.Empty;
    public bool IsParsed { get; set; }
    public string? SourcePost { get; set; }
    public string? SuggestedActionRaw { get; set; }
    public ObservationEditorModel Draft { get; set; } = new();
    public List<ObservationRadioTagSuggestionRow> SuggestedTags { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public static ObservationRadioDrawerModel CreateEmpty()
        => new()
        {
            Draft = new ObservationEditorModel
            {
                ObservedDate = DateTime.Now
            }
        };
}
