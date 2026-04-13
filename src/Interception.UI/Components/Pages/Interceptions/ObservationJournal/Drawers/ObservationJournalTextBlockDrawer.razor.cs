//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.TextBlock;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Pages.Interceptions.ObservationJournal.Drawers;

public partial class ObservationJournalTextBlockDrawer : ComponentBase
{
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionSuggestionService InterceptionSuggestionService { get; set; } = default!;
    [Inject] private IParticipantRoleService ParticipantRoleService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;
    [Inject] private ICommandContourService CommandContourService { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public IReadOnlyList<InterceptionActionListItemDto> Actions { get; set; } = [];
    [Parameter] public EventCallback OnSaved { get; set; }

    private string? _rawText = null;
    private string? _parseError = null;
    private TextBlockParseResult? _parsed = null;
    private InterceptionFormDto? _form = null;
    private string? _newLabel = null;
    private bool _saving;
    private readonly HashSet<int> _participantOpen = [];
    private readonly Dictionary<int, IReadOnlyList<ParticipantSuggestionDto>> _participantSuggestions = [];
    private IReadOnlyList<string> _roleSuggestions = [];
    private bool _rolesLoaded;
    private bool _directiveEnabled;
    private bool _directiveLoading;
    private IReadOnlyList<CommandContourOptionDto> _directiveOptions = [];
    private CommandContourSaveDto _directiveForm = CreateDefaultDirectiveForm();
    private string? _directiveFromSelection;
    private string? _directiveToSelection;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen)
        {
            ResetState(keepRoleCatalog: true);
            return;
        }

        if (_rolesLoaded)
            return;

        _roleSuggestions = [.. (await ParticipantRoleService.GetAllAsync())
        .Select(x => x.Name)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        _rolesLoaded = true;
    }

    private void OnDrawerClosed() => ResetState(keepRoleCatalog: true);

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
        await TryResolveDivisionByFrequencyAfterParseAsync(_form);
        _newLabel = null;
        _participantOpen.Clear();
        _participantSuggestions.Clear();

        await PopulateParticipantRolesAsync(_form);
        await LoadDirectiveOptionsAsync();
    }

    private async Task TryResolveDivisionByFrequencyAfterParseAsync(InterceptionFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Frequency))
            return;

        var frequency = form.Frequency.Trim();
        var suggestions = await InterceptionSuggestionService.GetFrequencyWithDivisionAsync(frequency);
        var matched = suggestions.FirstOrDefault(x =>
            string.Equals(x.Frequency, frequency, StringComparison.OrdinalIgnoreCase));

        if (matched is null || !SemanticValueExtensions.IsMeaningful(matched.Division))
            return;

        // Для текст-блоку значення підрозділу з реєстру (по ключу частоти) має пріоритет
        // над тим, що витягнув парсер. Це дозволяє зберігати поточний "snapshot" контексту
        // навіть якщо історичний/помилковий підрозділ присутній у сирому тексті.
        form.Division = matched.Division;
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
        participant.Role = NormalizeRoleFromCatalog(suggestion.Role) ?? suggestion.Role;
        participant.IsUnknown = false;

        if (string.IsNullOrWhiteSpace(_form?.Division) && !string.IsNullOrWhiteSpace(suggestion.Division))
            _form!.Division = suggestion.Division;

        if (string.IsNullOrWhiteSpace(_form?.Frequency) && !string.IsNullOrWhiteSpace(suggestion.Frequency))
            _form!.Frequency = suggestion.Frequency;

        _participantSuggestions.Remove(participant.Ordinal);
        _participantOpen.Remove(participant.Ordinal);
    }

    private void OnParticipantRoleInput(ParticipantFormDto participant, string? value)
        => participant.Role = value;

    private void OnParticipantRoleChanged(ParticipantFormDto participant, string? value)
        => participant.Role = NormalizeRoleFromCatalog(value) ?? value?.Trim();

    private string? NormalizeRoleFromCatalog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var matched = _roleSuggestions.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        return matched ?? trimmed;
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

        if (_directiveEnabled && !HasDirectiveSelection())
        {
            Toasts.Warning("Неповний зв'язок", "Оберіть обидві особи для блоку «Зв'язок керування» або вимкніть цей блок.");
            return;
        }

        _saving = true;
        try
        {
            var observationId = await InterceptionCommandService.CreateAsync(_form, "operator");
            Toasts.Success("Збережено", $"Запис від {ConverterDateTimeExtensions.ToDisplay(_form.ObservedDate):dd.MM HH:mm} створено.");
            await SaveDirectiveRelationAsync(observationId);

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

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);

    private void ResetState(bool keepRoleCatalog = false)
    {
        _rawText = null;
        _parseError = null;
        _parsed = null;
        _form = null;
        _newLabel = null;
        _saving = false;
        _participantOpen.Clear();
        _participantSuggestions.Clear();
        _directiveEnabled = false;
        _directiveLoading = false;
        _directiveOptions = [];
        _directiveForm = CreateDefaultDirectiveForm();
        _directiveFromSelection = null;
        _directiveToSelection = null;

        if (!keepRoleCatalog)
        {
            _roleSuggestions = [];
            _rolesLoaded = false;
        }
    }

    private async Task OnDirectiveEnabledChanged(bool value)
    {
        _directiveEnabled = value;
        if (!_directiveEnabled)
            return;

        await LoadDirectiveOptionsAsync();

        if (HasNamedParticipants() && _directiveOptions.Count == 0)
        {
            Toasts.Info(
                "Контур керування",
                "Для вибору в контурі потрібні особи, які вже оновлені/підтверджені у реєстрі осіб.");
        }
    }

    private async Task LoadDirectiveOptionsAsync()
    {
        if (_form is null)
        {
            _directiveOptions = [];
            return;
        }

        _directiveLoading = true;
        try
        {
            var names = _form.Participants
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _directiveOptions = await CommandContourService.GetIdentityOptionsAsync(names);
        }
        catch (Exception ex)
        {
            _directiveOptions = [];
            Toasts.Warning("Контур керування", $"Не вдалося завантажити варіанти осіб: {ex.Message}");
        }
        finally
        {
            _directiveLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool HasNamedParticipants()
        => _form?.Participants.Any(x => !string.IsNullOrWhiteSpace(x.Name)) == true;

    private async Task SaveDirectiveRelationAsync(Guid observationId)
    {
        if (!_directiveEnabled)
            return;

        try
        {
            _directiveForm.SourceObservationId = observationId;
            _directiveForm.IsManual = true;
            await CommandContourService.SaveAsync(_directiveForm);
            Toasts.Success("Контур керування", "Зв'язок керування зафіксовано.");
        }
        catch (Exception ex)
        {
            Toasts.Warning("Контур керування", $"Observation збережено, але зв'язок не зафіксовано: {ex.Message}");
        }
    }

    private bool HasDirectiveSelection()
    {
        var hasFrom = _directiveForm.FromCanonicalPersonId.HasValue || _directiveForm.FromResolvedParticipantId.HasValue;
        var hasTo = _directiveForm.ToCanonicalPersonId.HasValue || _directiveForm.ToResolvedParticipantId.HasValue;
        return hasFrom && hasTo;
    }

    private void OnDirectiveFromSelectionChanged(string? value)
    {
        _directiveFromSelection = value;
        ApplyDirectiveSelection(_directiveFromSelection, isFrom: true);
    }

    private void OnDirectiveToSelectionChanged(string? value)
    {
        _directiveToSelection = value;
        ApplyDirectiveSelection(_directiveToSelection, isFrom: false);
    }

    private void ApplyDirectiveSelection(string? value, bool isFrom)
    {
        if (isFrom)
        {
            _directiveForm.FromCanonicalPersonId = null;
            _directiveForm.FromResolvedParticipantId = null;
        }
        else
        {
            _directiveForm.ToCanonicalPersonId = null;
            _directiveForm.ToResolvedParticipantId = null;
        }

        if (string.IsNullOrWhiteSpace(value))
            return;

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
            return;

        var isCanonical = string.Equals(parts[0], "canonical", StringComparison.OrdinalIgnoreCase);

        if (isFrom)
        {
            if (isCanonical)
                _directiveForm.FromCanonicalPersonId = id;
            else
                _directiveForm.FromResolvedParticipantId = id;
        }
        else
        {
            if (isCanonical)
                _directiveForm.ToCanonicalPersonId = id;
            else
                _directiveForm.ToResolvedParticipantId = id;
        }
    }

    private static CommandContourSaveDto CreateDefaultDirectiveForm()
        => new()
        {
            RelationType = CommandContourRelationTypeDto.Command,
            Confidence = CommandContourConfidenceDto.High,
            IsManual = true
        };
}
