//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.TextBlock;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionTextBlockDrawer : ComponentBase
{
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionSuggestionService InterceptionSuggestionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public IReadOnlyList<InterceptionAction> Actions { get; set; } = [];
    [Parameter] public EventCallback OnSaved { get; set; }

    private string? _rawText = null;
    private string? _parseError = null;
    private TextBlockParseResult? _parsed = null;
    private InterceptionFormDto? _form = null;
    private string? _newLabel = null;
    private bool _saving;
    private readonly HashSet<int> _participantOpen = [];
    private readonly Dictionary<int, IReadOnlyList<ParticipantSuggestionDto>> _participantSuggestions = [];

    private void OnDrawerClosed() => ResetState();

    private async Task ParseAsync()
    {
        _parseError = null;

        var result = TextBlockParser.Parse(_rawText);

        if (!result.IsSuccess)
        {
            _parseError = result.Error;
            _parsed = null;
            _form = null;
            return;
        }

        _parsed = result;
        _form = BuildForm(result);
        _newLabel = null;
        _participantOpen.Clear();
        _participantSuggestions.Clear();

        await PopulateParticipantRolesAsync(_form);
    }

    private static InterceptionFormDto BuildForm(TextBlockParseResult r)
    {
        var ordinal = 1;
        var participants = new List<ParticipantFormDto>
        {
            new()
            {
                Ordinal = ordinal++,
                Name = r.Initiator,
                IsUnknown = r.Initiator is null
            }
        };

        foreach (var name in r.Responders)
        {
            participants.Add(new ParticipantFormDto
            {
                Ordinal = ordinal++,
                Name = name,
                IsUnknown = name is null
            });
        }

        if (participants.Count == 0)
        {
            participants.Add(new ParticipantFormDto { Ordinal = 1, IsUnknown = true });
            participants.Add(new ParticipantFormDto { Ordinal = 2, IsUnknown = true });
        }

        var observedDate = r.ObservedDate.HasValue
            ? ConverterDateTimeExtensions.ToUtc(r.ObservedDate.Value)
            : ConverterDateTimeExtensions.Now;

        return new InterceptionFormDto
        {
            ObservedDate = observedDate,
            Frequency = r.Frequency,
            Division = r.Division,
            VectorSignal = r.VectorSignal,
            Note = r.Note,
            InterceptionActionId = Guid.Empty,
            Participants = participants,
        };
    }

    private void OnObservedDateChange(ChangeEventArgs e)
    {
        if (_form is null)
            return;

        var parsed = ConverterDateTimeExtensions.Parse(e.Value?.ToString());
        if (parsed.HasValue)
            _form.ObservedDate = parsed.Value;
    }

    private void OnActionChange(ChangeEventArgs e)
    {
        if (_form is null)
            return;

        if (Guid.TryParse(e.Value?.ToString(), out var id))
            _form.InterceptionActionId = id;
    }

    private void OnUnknownToggle(ParticipantFormDto participant, bool isUnknown)
    {
        participant.IsUnknown = isUnknown;

        if (isUnknown)
        {
            participant.Name = null;
            participant.Role = null;
            _participantSuggestions.Remove(participant.Ordinal);
            _participantOpen.Remove(participant.Ordinal);
        }
    }

    private async Task OnParticipantNameInput(ParticipantFormDto participant, string? value)
    {
        participant.Name = value;

        if (participant.IsUnknown || string.IsNullOrWhiteSpace(value))
        {
            _participantSuggestions.Remove(participant.Ordinal);
            _participantOpen.Remove(participant.Ordinal);
            return;
        }

        var suggestions = await InterceptionSuggestionService.GetParticipantSuggestionsAsync(
            value,
            _form?.Frequency,
            _form?.Division);

        _participantSuggestions[participant.Ordinal] = suggestions;

        if (suggestions.Count > 0)
            _participantOpen.Add(participant.Ordinal);
        else
            _participantOpen.Remove(participant.Ordinal);
    }

    private async Task OnParticipantNameChangedAsync(ParticipantFormDto participant, string? value)
    {
        participant.Name = value;

        if (participant.IsUnknown || string.IsNullOrWhiteSpace(value))
            return;

        var suggestions = await InterceptionSuggestionService.GetParticipantSuggestionsAsync(
            value.Trim(),
            _form?.Frequency,
            _form?.Division);

        _participantSuggestions[participant.Ordinal] = suggestions;

        var matched = ResolveParticipantSuggestion(value.Trim(), suggestions);
        if (matched is not null)
        {
            ApplyParticipantSuggestion(participant, matched);
            return;
        }

        if (suggestions.Count > 0)
            _participantOpen.Add(participant.Ordinal);
    }

    private void AddParticipant()
    {
        if (_form is null)
            return;

        var next = _form.Participants.Count == 0
            ? 1
            : _form.Participants.Max(p => p.Ordinal) + 1;

        _form.Participants.Add(new ParticipantFormDto
        {
            Ordinal = next,
            IsUnknown = true
        });
    }

    private void RemoveParticipant(ParticipantFormDto participant)
    {
        if (_form is null)
            return;

        _form.Participants.Remove(participant);
        _participantSuggestions.Remove(participant.Ordinal);
        _participantOpen.Remove(participant.Ordinal);
    }

    private void AddLabel()
    {
        if (_form is null)
            return;

        var label = NormalizeLabel(_newLabel);
        if (label is null)
            return;

        if (_form.Labels.Any(x => string.Equals(x, label, StringComparison.OrdinalIgnoreCase)))
        {
            _newLabel = null;
            return;
        }

        _form.Labels.Add(label);
        _newLabel = null;
    }

    private void RemoveLabel(string label)
    {
        if (_form is null)
            return;

        var existing = _form.Labels.FirstOrDefault(x => string.Equals(x, label, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            _form.Labels.Remove(existing);
    }

    private void OnLabelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "," or ";")
            AddLabel();
    }

    private static string? NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }

    private async Task PopulateParticipantRolesAsync(InterceptionFormDto form)
    {
        foreach (var participant in form.Participants.Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name)))
            await TryPopulateParticipantRoleAsync(participant);
    }

    private async Task TryPopulateParticipantRoleAsync(ParticipantFormDto participant)
    {
        if (string.IsNullOrWhiteSpace(participant.Name))
            return;

        var suggestions = await InterceptionSuggestionService.GetParticipantSuggestionsAsync(
            participant.Name.Trim(),
            _form?.Frequency,
            _form?.Division);

        _participantSuggestions[participant.Ordinal] = suggestions;

        var matched = ResolveParticipantSuggestion(participant.Name.Trim(), suggestions);
        if (matched is not null)
        {
            ApplyParticipantSuggestion(participant, matched);
            return;
        }

        if (suggestions.Count > 0)
            _participantOpen.Add(participant.Ordinal);
    }

    private ParticipantSuggestionDto? ResolveParticipantSuggestion(
        string name,
        IReadOnlyList<ParticipantSuggestionDto> suggestions)
    {
        var exact = suggestions
            .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var exactByFrequency = exact
            .Where(x => FrequencyMatchesSuggestion(x, _form?.Frequency))
            .ToList();

        var exactByFullContext = exactByFrequency
            .Where(x => DivisionMatchesSuggestion(x, _form?.Division))
            .ToList();

        if (exactByFullContext.Count == 1)
            return exactByFullContext[0];

        if (exactByFullContext.Count == 0 && exactByFrequency.Count == 1)
            return exactByFrequency[0];

        if (exactByFullContext.Count == 0 && exactByFrequency.Count == 0 && exact.Count == 1)
            return exact[0];

        return null;
    }

    private static bool FrequencyMatchesSuggestion(ParticipantSuggestionDto suggestion, string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            return true;

        return string.Equals(suggestion.Frequency, frequency, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DivisionMatchesSuggestion(ParticipantSuggestionDto suggestion, string? division)
    {
        if (string.IsNullOrWhiteSpace(division))
            return true;

        return string.Equals(suggestion.Division, division, StringComparison.OrdinalIgnoreCase);
    }

    private void CloseParticipantSuggestions(int ordinal)
        => _participantOpen.Remove(ordinal);

    private void ApplyParticipantSuggestion(ParticipantFormDto participant, ParticipantSuggestionDto suggestion)
    {
        participant.Name = suggestion.Name;
        participant.Role = suggestion.Role;
        participant.IsUnknown = false;

        if (string.IsNullOrWhiteSpace(_form?.Division) && !string.IsNullOrWhiteSpace(suggestion.Division))
            _form!.Division = suggestion.Division;

        if (string.IsNullOrWhiteSpace(_form?.Frequency) && !string.IsNullOrWhiteSpace(suggestion.Frequency))
            _form!.Frequency = suggestion.Frequency;

        _participantSuggestions.Remove(participant.Ordinal);
        _participantOpen.Remove(participant.Ordinal);
    }

    private async Task SaveAsync()
    {
        if (_form is null)
            return;

        if (_form.InterceptionActionId == Guid.Empty)
        {
            Toasts.Warning("Не обрано дію", "Оберіть дію перед збереженням.");
            return;
        }

        _saving = true;
        try
        {
            await InterceptionCommandService.CreateAsync(_form, "operator");
            Toasts.Success(
                "Збережено",
                $"Запис від {ConverterDateTimeExtensions.ToDisplay(_form.ObservedDate):dd.MM HH:mm} створено.");

            ResetState();
            await CloseAsync();
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка збереження", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void ResetState()
    {
        _rawText = null;
        _parseError = null;
        _parsed = null;
        _form = null;
        _newLabel = null;
        _participantOpen.Clear();
        _participantSuggestions.Clear();
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
