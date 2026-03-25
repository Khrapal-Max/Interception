//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionFormDrawer : ComponentBase
{
    [Inject] private IInterceptionSuggestionService InterceptionSuggestionService { get; set; } = default!;
    [Inject] private IInterceptionQueryService InterceptionQueryService { get; set; } = default!;
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionActionService ActionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public Guid? EditingId { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private InterceptionFormDto? _form;
    private IReadOnlyList<InterceptionAction> _actions = [];
    private IReadOnlyList<FrequencySuggestionDto> _frequencySuggestions = [];
    private IReadOnlyList<string> _vectorSuggestions = [];
    private bool _saving;
    private bool _initialized;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen) { _initialized = false; return; }
        if (_initialized) return;
        _initialized = true;

        _actions = await ActionService.GetAllAsync();

        if (EditingId.HasValue)
            await InitEditAsync(EditingId.Value);
        else
            await InitCreateAsync();
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _form = null;
    }

    // -------------------------------------------------------------------------
    // Ініціалізація
    // -------------------------------------------------------------------------

    private async Task InitCreateAsync()
    {
        _frequencySuggestions = await InterceptionSuggestionService.GetFrequencyWithDivisionAsync();

        var topFreq = _frequencySuggestions.Count > 0 ? _frequencySuggestions[0] : null;

        // FIX: frequency — другий параметр, не перший
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: topFreq?.Frequency);

        _form = new InterceptionFormDto
        {
            ObservedDate = DateTimeConverter.ToDisplay(DateTimeConverter.Now),
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

        // FIX: frequency — другий параметр
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: message.Frequency);

        _form = new InterceptionFormDto
        {
            ObservedDate = DateTimeConverter.ToDisplay(message.ObservedDate),
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
                    Ordinal   = pt.Ordinal,
                    Name      = pt.Name,
                    Role      = pt.Role,
                    IsUnknown = pt.IsUnknown
                })],
            Labels = [.. message.Labels.Select(l => l.NameLabel)]
        };
    }

    // -------------------------------------------------------------------------
    // Suggestions
    // -------------------------------------------------------------------------

    internal async Task SearchFrequencyAsync(string? query)
    {
        _frequencySuggestions = await InterceptionSuggestionService
            .GetFrequencyWithDivisionAsync(query);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Оператор вибрав частоту з dropdown.
    /// Оновлюємо VectorSuggestions контекстно — тільки вектори цієї частоти.
    /// </summary>
    internal async Task ApplyFrequencySuggestion(FrequencySuggestionDto s)
    {
        // FIX: frequency — другий параметр
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: null, frequency: s.Frequency);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Оператор набирає текст у полі вектора.
    /// Шукаємо в межах поточної частоти форми (контекстний пошук).
    /// </summary>
    internal async Task SearchVectorAsync(string? query)
    {
        // FIX: передаємо поточну частоту як контекст
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(query: query, frequency: _form?.Frequency);
        await InvokeAsync(StateHasChanged);
    }

    internal Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantsAsync(string? query)
        => InterceptionSuggestionService.GetParticipantSuggestionsAsync(query);

    // -------------------------------------------------------------------------
    // Save / Close
    // -------------------------------------------------------------------------

    private async Task SaveAsync()
    {
        if (_form is null) return;

        _saving = true;
        try
        {
            if (EditingId.HasValue)
            {
                await InterceptionCommandService.UpdateAsync(EditingId.Value, _form);
                Toasts.Success("Оновлено", $"Запис від {_form.ObservedDate:dd.MM HH:mm} оновлено.");
            }
            else
            {
                await InterceptionCommandService.CreateAsync(_form, "operator");
                Toasts.Success("Збережено", $"Запис від {_form.ObservedDate:dd.MM HH:mm} створено.");
            }

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
}
