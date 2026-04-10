//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Pages.Interceptions.InterceptionRegistry.Drawers;

public partial class InterceptionFormBody : ComponentBase
{
    [Parameter, EditorRequired]
    public InterceptionFormDto Form { get; set; } = default!;

    [Parameter] public IReadOnlyList<InterceptionAction> Actions { get; set; } = [];
    [Parameter] public IReadOnlyList<FrequencySuggestionDto> FrequencySuggestions { get; set; } = [];
    [Parameter] public IReadOnlyList<string> VectorSuggestions { get; set; } = [];
    [Parameter] public IReadOnlyList<string> RoleSuggestions { get; set; } = [];

    [Parameter] public EventCallback<string?> OnFrequencySearch { get; set; }
    [Parameter] public EventCallback<FrequencySuggestionDto> OnFrequencySelected { get; set; }
    [Parameter] public EventCallback<string?> OnVectorSearch { get; set; }
    [Parameter] public Func<string?, string?, string?, Task<IReadOnlyList<ParticipantSuggestionDto>>>? OnParticipantSearch { get; set; }

    private bool _freqOpen;
    private bool _vecOpen;
    private readonly HashSet<int> _participantOpen = [];
    private readonly Dictionary<int, IReadOnlyList<ParticipantSuggestionDto>> _participantSuggestions = [];
    private string? _newLabel;

    private void OnObservedDateChange(ChangeEventArgs e)
    {
        var parsed = ConverterDateTimeExtensions.Parse(e.Value?.ToString());
        if (parsed.HasValue)
            Form.ObservedDate = parsed.Value;
    }

    private void OnActionChange(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var id))
            Form.InterceptionActionId = id;
    }

    private async Task OnFrequencyInput(ChangeEventArgs e)
    {
        Form.Frequency = e.Value?.ToString();
        _freqOpen = true;
        await OnFrequencySearch.InvokeAsync(Form.Frequency);
    }

    private void OnFrequencyFocus()
    {
        if (FrequencySuggestions.Count > 0)
            _freqOpen = true;
    }

    private void OnFrequencyBlur() => _freqOpen = false;

    private async Task OnFrequencySuggestionSelected(FrequencySuggestionDto s)
    {
        Form.Frequency = s.Frequency;
        if (!string.IsNullOrWhiteSpace(s.Division))
            Form.Division = s.Division;
        if (!string.IsNullOrWhiteSpace(s.VectorSignal))
            Form.VectorSignal = s.VectorSignal;

        _freqOpen = false;
        await OnFrequencySelected.InvokeAsync(s);
    }

    private async Task OnVectorInput(ChangeEventArgs e)
    {
        Form.VectorSignal = e.Value?.ToString();
        _vecOpen = true;
        await OnVectorSearch.InvokeAsync(Form.VectorSignal);
    }

    private void OnVectorFocus()
    {
        if (VectorSuggestions.Count > 0)
            _vecOpen = true;
    }

    private void OnVectorBlur() => _vecOpen = false;

    private void AddParticipant()
    {
        var next = Form.Participants.Count == 0
            ? 1
            : Form.Participants.Max(p => p.Ordinal) + 1;
        Form.Participants.Add(new ParticipantFormDto { Ordinal = next, IsUnknown = true });
    }

    private void RemoveParticipant(ParticipantFormDto p)
    {
        Form.Participants.Remove(p);
        _participantSuggestions.Remove(p.Ordinal);
        _participantOpen.Remove(p.Ordinal);
    }

    private void OnUnknownToggle(ParticipantFormDto p, bool isUnknown)
    {
        p.IsUnknown = isUnknown;
        if (isUnknown)
        {
            p.Name = null;
            p.Role = null;
            _participantSuggestions.Remove(p.Ordinal);
            _participantOpen.Remove(p.Ordinal);
        }
    }

    private async Task OnParticipantNameInput(ParticipantFormDto p, string? value)
    {
        p.Name = value;

        if (OnParticipantSearch is null || string.IsNullOrWhiteSpace(value))
        {
            _participantSuggestions.Remove(p.Ordinal);
            _participantOpen.Remove(p.Ordinal);
            return;
        }

        _participantSuggestions[p.Ordinal] = await OnParticipantSearch(value, Form.Frequency, Form.Division);
        _participantOpen.Add(p.Ordinal);
    }

    private async Task OnParticipantNameChangedAsync(ParticipantFormDto p, string? value)
    {
        p.Name = value;

        if (OnParticipantSearch is null || string.IsNullOrWhiteSpace(value) || p.IsUnknown)
            return;

        var suggestions = await OnParticipantSearch(value, Form.Frequency, Form.Division);
        _participantSuggestions[p.Ordinal] = suggestions;

        var typedName = value.Trim();

        var exact = suggestions
            .Where(x => string.Equals(x.Name, typedName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var exactWithContext = exact
            .Where(x => ContextMatchesSuggestion(x, Form.Frequency, Form.Division))
            .ToList();

        if (exactWithContext.Count == 1)
        {
            ApplyParticipantSuggestion(p, exactWithContext[0]);
            return;
        }

        if (exactWithContext.Count == 0 && exact.Count == 1)
        {
            ApplyParticipantSuggestion(p, exact[0]);
            return;
        }

        if (exact.Count > 1 || exactWithContext.Count > 1)
            _participantOpen.Add(p.Ordinal);
    }

    private static bool ContextMatchesSuggestion(ParticipantSuggestionDto suggestion, string? frequency, string? division)
    {
        if (!string.IsNullOrWhiteSpace(frequency)
            && !string.Equals(suggestion.Frequency, frequency, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(division)
            && !string.Equals(suggestion.Division, division, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void CloseParticipantSuggestions(int ordinal)
        => _participantOpen.Remove(ordinal);

    private void ApplyParticipantSuggestion(ParticipantFormDto p, ParticipantSuggestionDto s)
    {
        p.Name = s.Name;
        p.Role = NormalizeRoleFromCatalog(s.Role) ?? s.Role;
        p.IsUnknown = false;

        if (string.IsNullOrWhiteSpace(Form.Division) && !string.IsNullOrWhiteSpace(s.Division))
            Form.Division = s.Division;

        if (string.IsNullOrWhiteSpace(Form.Frequency) && !string.IsNullOrWhiteSpace(s.Frequency))
            Form.Frequency = s.Frequency;

        _participantSuggestions.Remove(p.Ordinal);
        _participantOpen.Remove(p.Ordinal);
    }

    private void OnParticipantRoleInput(ParticipantFormDto p, string? value)
        => p.Role = value;

    private void OnParticipantRoleChanged(ParticipantFormDto p, string? value)
        => p.Role = NormalizeRoleFromCatalog(value) ?? value?.Trim();

    private string? NormalizeRoleFromCatalog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var matched = RoleSuggestions.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        return matched ?? trimmed;
    }

    private void AddLabel()
    {
        if (string.IsNullOrWhiteSpace(_newLabel))
            return;

        var norm = _newLabel.Trim();
        if (!Form.Labels.Contains(norm, StringComparer.OrdinalIgnoreCase))
            Form.Labels.Add(norm);

        _newLabel = null;
    }

    private void OnLabelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            AddLabel();
    }
}
