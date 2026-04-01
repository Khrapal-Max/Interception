//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Dtos;
using Interception.Domain.Entities;
using Interception.Domain.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.Web.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionFormBody : ComponentBase
{
    // -------------------------------------------------------------------------
    // Parameters
    // -------------------------------------------------------------------------

    [Parameter, EditorRequired]
    public InterceptionFormDto Form { get; set; } = default!;

    [Parameter] public IReadOnlyList<InterceptionAction> Actions { get; set; } = [];
    [Parameter] public IReadOnlyList<FrequencySuggestionDto> FrequencySuggestions { get; set; } = [];
    [Parameter] public IReadOnlyList<string> VectorSuggestions { get; set; } = [];

    [Parameter] public EventCallback<string?> OnFrequencySearch { get; set; }
    [Parameter] public EventCallback<FrequencySuggestionDto> OnFrequencySelected { get; set; }
    [Parameter] public EventCallback<string?> OnVectorSearch { get; set; }
    [Parameter] public Func<string?, Task<IReadOnlyList<ParticipantSuggestionDto>>>? OnParticipantSearch { get; set; }

    // -------------------------------------------------------------------------
    // Стан autocomplete
    // -------------------------------------------------------------------------

    private bool _freqOpen;
    private bool _vecOpen;

    // Які поля імені учасників зараз відкриті (ключ = Ordinal)
    private readonly HashSet<int> _participantOpen = [];

    private readonly Dictionary<int, IReadOnlyList<ParticipantSuggestionDto>> _participantSuggestions = [];
    private string? _newLabel;

    // -------------------------------------------------------------------------
    // Дата
    // -------------------------------------------------------------------------

    private void OnObservedDateChange(ChangeEventArgs e)
    {
        var parsed = ConverterDateTimeExtensions.Parse(e.Value?.ToString());
        if (parsed.HasValue)
            Form.ObservedDate = parsed.Value;
    }

    // -------------------------------------------------------------------------
    // Дія
    // -------------------------------------------------------------------------

    private void OnActionChange(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var id))
            Form.InterceptionActionId = id;
    }

    // -------------------------------------------------------------------------
    // Частота
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Вектор
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Учасники — autocomplete
    // -------------------------------------------------------------------------

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

        _participantSuggestions[p.Ordinal] = await OnParticipantSearch(value);
        _participantOpen.Add(p.Ordinal);
    }

    private async Task OnParticipantNameChangedAsync(ParticipantFormDto p, string? value)
    {
        p.Name = value;

        if (OnParticipantSearch is null || string.IsNullOrWhiteSpace(value) || p.IsUnknown)
            return;

        var suggestions = await OnParticipantSearch(value);
        var matched = suggestions.FirstOrDefault(x =>
            string.Equals(x.Name, value.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matched is not null && !string.IsNullOrWhiteSpace(matched.Role))
            p.Role = matched.Role;
    }

    private void CloseParticipantSuggestions(int ordinal)
        => _participantOpen.Remove(ordinal);

    private void ApplyParticipantSuggestion(ParticipantFormDto p, ParticipantSuggestionDto s)
    {
        p.Name = s.Name;
        p.Role = s.Role;
        p.IsUnknown = false;
        _participantSuggestions.Remove(p.Ordinal);
        _participantOpen.Remove(p.Ordinal);
    }

    // -------------------------------------------------------------------------
    // Мітки
    // -------------------------------------------------------------------------

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
