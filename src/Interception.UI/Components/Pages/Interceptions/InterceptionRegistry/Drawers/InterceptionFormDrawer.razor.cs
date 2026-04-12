//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Pages.Interceptions.InterceptionRegistry.Drawers;

public partial class InterceptionFormDrawer : ComponentBase
{
    [Inject] private IInterceptionSuggestionService InterceptionSuggestionService { get; set; } = default!;
    [Inject] private IInterceptionQueryService InterceptionQueryService { get; set; } = default!;
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionActionService ActionService { get; set; } = default!;
    [Inject] private IParticipantRoleService ParticipantRoleService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;
    [Inject] private ICommandContourService CommandContourService { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public Guid? EditingId { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private InterceptionFormDto? _form;
    private IReadOnlyList<InterceptionActionListItemDto> _actions = [];
    private IReadOnlyList<FrequencySuggestionDto> _frequencySuggestions = [];
    private IReadOnlyList<string> _vectorSuggestions = [];
    private IReadOnlyList<string> _roleSuggestions = [];
    private bool _saving;
    private bool _initialized;
    private bool _directiveEnabled;
    private bool _directiveLoading;
    private IReadOnlyList<CommandContourOptionDto> _directiveOptions = [];
    private CommandContourSaveDto _directiveForm = CreateDefaultDirectiveForm();
    private string? _directiveFromSelection;
    private string? _directiveToSelection;
    private bool _freqOpen;
    private bool _vecOpen;
    private readonly HashSet<int> _participantOpen = [];
    private readonly Dictionary<int, IReadOnlyList<ParticipantSuggestionDto>> _participantSuggestions = [];
    private string? _newLabel;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen)
        {
            _initialized = false;
            return;
        }

        if (_initialized)
            return;

        _initialized = true;
        _actions = await ActionService.GetAllAsync();
        _roleSuggestions = [.. (await ParticipantRoleService.GetAllAsync())
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        if (EditingId.HasValue)
            await InitEditAsync(EditingId.Value);
        else
            await InitCreateAsync();

        await LoadDirectiveOptionsAsync();
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _form = null;
        _freqOpen = false;
        _vecOpen = false;
        _participantOpen.Clear();
        _participantSuggestions.Clear();
        _newLabel = null;
        _directiveEnabled = false;
        _directiveLoading = false;
        _directiveOptions = [];
        _directiveForm = CreateDefaultDirectiveForm();
        _directiveFromSelection = null;
        _directiveToSelection = null;
    }

    private async Task InitCreateAsync()
    {
        _frequencySuggestions = await InterceptionSuggestionService.GetFrequencyWithDivisionAsync();

        var topFreq = _frequencySuggestions.Count > 0 ? _frequencySuggestions[0] : null;

        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: topFreq?.Frequency);

        _form = new InterceptionFormDto
        {
            ObservedDate = ConverterDateTimeExtensions.ToDisplay(ConverterDateTimeExtensions.Now),
            Frequency = topFreq?.Frequency,
            Division = topFreq?.Division,
            VectorSignal = topFreq?.VectorSignal,
            InterceptionActionId = _actions.Count == 1 ? _actions[0].Id : Guid.Empty,
            Participants =
            [
                new ParticipantFormDto { Ordinal = 1, IsUnknown = true },
                new ParticipantFormDto { Ordinal = 2, IsUnknown = true },
            ]
        };
    }

    private async Task InitEditAsync(Guid id)
    {
        var message = await InterceptionQueryService.GetByIdAsync(id);
        if (message is null)
        {
            Toasts.Warning("Не знайдено", "Запис вже видалено або недоступний.");
            await CloseAsync();
            return;
        }

        _frequencySuggestions = await InterceptionSuggestionService
            .GetFrequencyWithDivisionAsync(message.Frequency);

        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: message.Frequency);

        _form = new InterceptionFormDto
        {
            ObservedDate = ConverterDateTimeExtensions.ToDisplay(message.ObservedDate),
            Frequency = message.Frequency,
            Division = message.Division,
            PointSignal = message.PointSignal,
            VectorSignal = message.VectorSignal,
            InterceptionActionId = message.InterceptionActionId ?? Guid.Empty,
            Note = message.Note,
            Participants = [.. message.Participants
                .OrderBy(pt => pt.Ordinal)
                .Select(pt => new ParticipantFormDto
                {
                    Ordinal = pt.Ordinal,
                    Name = pt.Name,
                    Role = pt.Role,
                    IsUnknown = pt.IsUnknown
                })],
            Labels = [.. message.Labels]
        };
    }

    internal async Task SearchFrequencyAsync(string? query)
    {
        _frequencySuggestions = await InterceptionSuggestionService
            .GetFrequencyWithDivisionAsync(query);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task ApplyFrequencySuggestion(FrequencySuggestionDto s)
    {
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: s.Frequency);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task SearchVectorAsync(string? query)
    {
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: query, frequency: _form?.Frequency);
        await InvokeAsync(StateHasChanged);
    }

    internal Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantsAsync(string? query, string? frequency, string? division)
        => InterceptionSuggestionService.GetParticipantSuggestionsAsync(query, frequency, division);

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

    private async Task OnFrequencyInput(ChangeEventArgs e)
    {
        if (_form is null)
            return;

        _form.Frequency = e.Value?.ToString();
        _freqOpen = true;
        await SearchFrequencyAsync(_form.Frequency);
    }

    private void OnFrequencyFocus()
    {
        if (_frequencySuggestions.Count > 0)
            _freqOpen = true;
    }

    private void OnFrequencyBlur() => _freqOpen = false;

    private async Task OnFrequencySuggestionSelected(FrequencySuggestionDto s)
    {
        if (_form is null)
            return;

        _form.Frequency = s.Frequency;
        if (!string.IsNullOrWhiteSpace(s.Division))
            _form.Division = s.Division;
        if (!string.IsNullOrWhiteSpace(s.VectorSignal))
            _form.VectorSignal = s.VectorSignal;

        _freqOpen = false;
        await ApplyFrequencySuggestion(s);
    }

    private async Task OnVectorInput(ChangeEventArgs e)
    {
        if (_form is null)
            return;

        _form.VectorSignal = e.Value?.ToString();
        _vecOpen = true;
        await SearchVectorAsync(_form.VectorSignal);
    }

    private void OnVectorFocus()
    {
        if (_vectorSuggestions.Count > 0)
            _vecOpen = true;
    }

    private void OnVectorBlur() => _vecOpen = false;

    private void AddParticipant()
    {
        if (_form is null)
            return;

        var next = _form.Participants.Count == 0
            ? 1
            : _form.Participants.Max(p => p.Ordinal) + 1;
        _form.Participants.Add(new ParticipantFormDto { Ordinal = next, IsUnknown = true });
    }

    private void RemoveParticipant(ParticipantFormDto participant)
    {
        if (_form is null)
            return;

        _form.Participants.Remove(participant);
        _participantSuggestions.Remove(participant.Ordinal);
        _participantOpen.Remove(participant.Ordinal);
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

        if (_form is null || string.IsNullOrWhiteSpace(value))
        {
            _participantSuggestions.Remove(participant.Ordinal);
            _participantOpen.Remove(participant.Ordinal);
            return;
        }

        _participantSuggestions[participant.Ordinal] = await SearchParticipantsAsync(value, _form.Frequency, _form.Division);
        _participantOpen.Add(participant.Ordinal);
    }

    private async Task OnParticipantNameChangedAsync(ParticipantFormDto participant, string? value)
    {
        participant.Name = value;

        if (_form is null || string.IsNullOrWhiteSpace(value) || participant.IsUnknown)
            return;

        var suggestions = await SearchParticipantsAsync(value, _form.Frequency, _form.Division);
        _participantSuggestions[participant.Ordinal] = suggestions;

        var typedName = value.Trim();
        var exact = suggestions
            .Where(x => string.Equals(x.Name, typedName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var exactWithContext = exact
            .Where(x => ContextMatchesSuggestion(x, _form.Frequency, _form.Division))
            .ToList();

        if (exactWithContext.Count == 1)
        {
            ApplyParticipantSuggestion(participant, exactWithContext[0]);
            return;
        }

        if (exactWithContext.Count == 0 && exact.Count == 1)
        {
            ApplyParticipantSuggestion(participant, exact[0]);
            return;
        }

        if (exact.Count > 1 || exactWithContext.Count > 1)
            _participantOpen.Add(participant.Ordinal);
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

    private void ApplyParticipantSuggestion(ParticipantFormDto participant, ParticipantSuggestionDto suggestion)
    {
        if (_form is null)
            return;

        participant.Name = suggestion.Name;
        participant.Role = NormalizeRoleFromCatalog(suggestion.Role) ?? suggestion.Role;
        participant.IsUnknown = false;

        if (string.IsNullOrWhiteSpace(_form.Division) && !string.IsNullOrWhiteSpace(suggestion.Division))
            _form.Division = suggestion.Division;

        if (string.IsNullOrWhiteSpace(_form.Frequency) && !string.IsNullOrWhiteSpace(suggestion.Frequency))
            _form.Frequency = suggestion.Frequency;

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

    private void AddLabel()
    {
        if (_form is null || string.IsNullOrWhiteSpace(_newLabel))
            return;

        var normalized = _newLabel.Trim();
        if (!_form.Labels.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            _form.Labels.Add(normalized);

        _newLabel = null;
    }

    private void OnLabelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            AddLabel();
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
            Guid savedId;
            if (EditingId.HasValue)
            {
                await InterceptionCommandService.UpdateAsync(EditingId.Value, _form);
                savedId = EditingId.Value;
                Toasts.Success("Оновлено", $"Запис від {ConverterDateTimeExtensions.ToDisplay(_form.ObservedDate):dd.MM HH:mm} оновлено.");
            }
            else
            {
                savedId = await InterceptionCommandService.CreateAsync(_form, "operator");
                Toasts.Success("Збережено", $"Запис від {ConverterDateTimeExtensions.ToDisplay(_form.ObservedDate):dd.MM HH:mm} створено.");
            }

            await SaveDirectiveRelationAsync(savedId);
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

    private async Task OnDirectiveEnabledChanged(bool value)
    {
        _directiveEnabled = value;
        if (_directiveEnabled)
            await LoadDirectiveOptionsAsync();
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

            if (names.Count > 0 && _directiveOptions.Count == 0)
            {
                Toasts.Info(
                    "Контур керування",
                    "Для вибору в контурі потрібні особи, які вже оновлені/підтверджені у реєстрі осіб.");
            }
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

    private bool HasDirectiveSelection()
    {
        var hasFrom = _directiveForm.FromCanonicalPersonId.HasValue || _directiveForm.FromResolvedParticipantId.HasValue;
        var hasTo = _directiveForm.ToCanonicalPersonId.HasValue || _directiveForm.ToResolvedParticipantId.HasValue;
        return hasFrom && hasTo;
    }

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
