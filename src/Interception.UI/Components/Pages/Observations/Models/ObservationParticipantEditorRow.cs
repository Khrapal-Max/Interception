namespace Interception.UI.Components.Pages.Observations.Models;

public sealed class ObservationParticipantEditorRow
{
    public Guid? Id { get; set; }
    public string? LabelRaw { get; set; }
    public bool IsUnknown { get; set; }
    public string? RoleRaw { get; set; }
    public int Ordinal { get; set; } = 1;

    public string? SuggestedKnownLabel { get; set; }
    public string? SuggestedKnownRole { get; set; }
    public int SuggestedKnownSeenCount { get; set; }
    public bool KnownLookupChecked { get; set; }
    public bool KnownSuggestionApplied { get; set; }

    public bool HasKnownSuggestion => !string.IsNullOrWhiteSpace(SuggestedKnownLabel);

    public void ClearKnownSuggestion()
    {
        SuggestedKnownLabel = null;
        SuggestedKnownRole = null;
        SuggestedKnownSeenCount = 0;
        KnownLookupChecked = false;
        KnownSuggestionApplied = false;
    }
}
