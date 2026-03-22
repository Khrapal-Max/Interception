//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionFilterDrawer : ComponentBase
{
    [Inject] private IInterceptionService InterceptionService { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public InterceptionFilter Filter { get; set; } = default!;
    [Parameter] public EventCallback<InterceptionFilter> OnApplied { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }

    private InterceptionFilter _model = new();
    private IReadOnlyList<string> _frequencySuggestions = [];
    private bool _initialized;

    private string? DateFromStr => _model.DateFrom?.ToString("yyyy-MM-ddTHH:mm");
    private string? DateToStr => _model.DateTo?.ToString("yyyy-MM-ddTHH:mm");

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen) { _initialized = false; return; }
        if (_initialized) return;
        _initialized = true;

        _model = Clone(Filter);

        // Фільтру потрібні тільки рядки частот — беремо Frequency з FrequencySuggestionDto
        _frequencySuggestions = await GetFrequencyStringsAsync();
    }

    private void OnDrawerClosed() => _initialized = false;

    private async Task OnFrequencyInput(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _model.Frequency = value;
        _frequencySuggestions = await GetFrequencyStringsAsync(value);
    }

    private void OnDateFromChange(string? value)
        => _model.DateFrom = DateTimeConverter.Parse(value);

    private void OnDateToChange(string? value)
        => _model.DateTo = DateTimeConverter.Parse(value);

    private async Task ApplyAsync()
    {
        await IsOpenChanged.InvokeAsync(false);
        await OnApplied.InvokeAsync(Clone(_model));
    }

    private async Task ResetAsync()
    {
        _model = new InterceptionFilter();
        await IsOpenChanged.InvokeAsync(false);
        await OnReset.InvokeAsync();
    }

    // -------------------------------------------------------------------------
    // Helper — фільтру потрібні тільки рядки, не повні DTO
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<string>> GetFrequencyStringsAsync(string? query = null)
    {
        var suggestions = await InterceptionService.GetFrequencyWithDivisionAsync(query);
        return [.. suggestions.Select(s => s.Frequency)];
    }

    private static InterceptionFilter Clone(InterceptionFilter? src)
        => src is null
            ? new InterceptionFilter()
            : new InterceptionFilter
            {
                DateFrom = src.DateFrom,
                DateTo = src.DateTo,
                Frequency = src.Frequency,
                ParticipantName = src.ParticipantName,
                LabelName = src.LabelName
            };
}
