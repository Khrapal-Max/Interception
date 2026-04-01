//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionFilterDrawer : ComponentBase
{
    [Inject] private IInterceptionSuggestionService InterceptionSuggestionService { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public InterceptionFilterDto Filter { get; set; } = default!;
    [Parameter] public EventCallback<InterceptionFilterDto> OnApplied { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }

    private InterceptionFilterDto _model = new();
    private IReadOnlyList<string> _frequencySuggestions = [];
    private IReadOnlyList<string> _vectorSuggestions = [];
    private bool _initialized;
    private bool _freqOpen;
    private bool _vecOpen;

    private string? DateFromStr => _model.DateFrom?.ToString("yyyy-MM-ddTHH:mm");
    private string? DateToStr => _model.DateTo?.ToString("yyyy-MM-ddTHH:mm");

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen) { _initialized = false; return; }
        if (_initialized) return;
        _initialized = true;

        _model = Clone(Filter);
        _frequencySuggestions = await GetFrequencyStringsAsync();

        // Якщо фільтр вже має частоту — підвантажуємо вектори для неї
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(frequency: _model.Frequency);
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _freqOpen = false;
        _vecOpen = false;
    }

    // -------------------------------------------------------------------------
    // Частота
    // -------------------------------------------------------------------------

    private async Task OnFrequencyInput(ChangeEventArgs e)
    {
        _model.Frequency = e.Value?.ToString();
        _freqOpen = true;
        _frequencySuggestions = await GetFrequencyStringsAsync(_model.Frequency);
    }

    private void OnFrequencyFocus()
    {
        if (_frequencySuggestions.Count > 0) _freqOpen = true;
    }

    private async Task OnFrequencySelected(string freq)
    {
        _model.Frequency = freq;
        _freqOpen = false;

        // Оновлюємо вектори контекстно для вибраної частоти
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(frequency: freq);
    }

    // -------------------------------------------------------------------------
    // Вектор
    // -------------------------------------------------------------------------

    private async Task OnVectorInput(ChangeEventArgs e)
    {
        _model.VectorSignal = e.Value?.ToString();
        _vecOpen = true;
        _vectorSuggestions = await InterceptionSuggestionService
            .GetVectorSignalSuggestionsAsync(
                query: _model.VectorSignal,
                frequency: _model.Frequency);
    }

    private void OnVectorFocus()
    {
        if (_vectorSuggestions.Count > 0) _vecOpen = true;
    }

    private void OnVectorSelected(string vec)
    {
        _model.VectorSignal = vec;
        _vecOpen = false;
    }

    // -------------------------------------------------------------------------
    // Дата
    // -------------------------------------------------------------------------

    private void OnDateFromChange(string? value)
        => _model.DateFrom = ConverterDateTimeExtensions.Parse(value);

    private void OnDateToChange(string? value)
        => _model.DateTo = ConverterDateTimeExtensions.Parse(value);

    // -------------------------------------------------------------------------
    // Apply / Reset
    // -------------------------------------------------------------------------

    private async Task ApplyAsync()
    {
        await IsOpenChanged.InvokeAsync(false);
        await OnApplied.InvokeAsync(Clone(_model));
    }

    private async Task ResetAsync()
    {
        _model = new InterceptionFilterDto();
        await IsOpenChanged.InvokeAsync(false);
        await OnReset.InvokeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<string>> GetFrequencyStringsAsync(string? query = null)
    {
        var suggestions = await InterceptionSuggestionService.GetFrequencyWithDivisionAsync(query);
        return [.. suggestions.Select(s => s.Frequency)];
    }

    private static InterceptionFilterDto Clone(InterceptionFilterDto? src)
        => src is null
            ? new InterceptionFilterDto()
            : new InterceptionFilterDto
            {
                DateFrom = src.DateFrom,
                DateTo = src.DateTo,
                Frequency = src.Frequency,
                VectorSignal = src.VectorSignal,
                ParticipantName = src.ParticipantName,
                LabelName = src.LabelName
            };
}
