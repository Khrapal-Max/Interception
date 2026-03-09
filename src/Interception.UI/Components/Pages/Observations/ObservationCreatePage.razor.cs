//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Pages.Observations;

/// <summary>
/// Окрема сторінка створення спостереження.
/// Ліва частина залишається компактною, а контекст дії скролиться лише всередині правої панелі.
/// </summary>
public partial class ObservationCreatePage : ComponentBase, IDisposable
{
    private const int SuggestMinChars = 2;
    private const int MaxParticipants = 3;
    private static readonly TimeSpan SuggestDebounce = TimeSpan.FromMilliseconds(250);

    [Inject] public IObservationWriteService WriteService { get; set; } = default!;
    [Inject] public IObservationLookupService LookupService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private DateTime _date = DateTime.Today;
    private short _dayPart = 1;

    private string? _layer;
    private string _actionRaw = string.Empty;
    private string? _rmRaw;
    private string? _pointRaw;
    private string? _locationRaw;
    private string? _districtRaw;
    private string? _note;

    private readonly List<ParticipantDraft> _participants = [];
    private readonly List<LayerRmSuggestionDto> _layerSuggestions = [];
    private readonly List<string> _districtSuggestions = [];
    private readonly List<ParticipantSuggestionDto> _participantSuggestions = [];
    private readonly List<ActionContextSuggestionDto> _actionContext = [];

    private bool _layerHasFocus;
    private bool _districtHasFocus;
    private int? _participantSuggestIndex;

    private bool _saving;
    private bool _duplicate;
    private bool _contextLoading;
    private string? _error;

    private CancellationTokenSource? _layerCts;
    private CancellationTokenSource? _districtCts;
    private CancellationTokenSource? _participantCts;
    private CancellationTokenSource? _contextCts;

    private bool ShowLayerSuggestions => _layerHasFocus && _layerSuggestions.Count > 0;
    private bool ShowDistrictSuggestions => _districtHasFocus && _districtSuggestions.Count > 0;
    private bool CanAddParticipant => _participants.Count < MaxParticipants;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        AddParticipant();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancelAndDispose(ref _layerCts);
        CancelAndDispose(ref _districtCts);
        CancelAndDispose(ref _participantCts);
        CancelAndDispose(ref _contextCts);
        GC.SuppressFinalize(this);
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void AddParticipant()
    {
        if (!CanAddParticipant)
            return;

        _participants.Add(new ParticipantDraft());
    }

    private void RemoveParticipant(int index)
    {
        if ((uint)index >= (uint)_participants.Count)
            return;

        _participants.RemoveAt(index);
        if (_participantSuggestIndex == index)
        {
            _participantSuggestIndex = null;
            _participantSuggestions.Clear();
        }

        _ = ReloadActionContextAsync();
    }

    private void OnLayerFocus() => _layerHasFocus = true;

    private async Task OnLayerBlurAsync()
    {
        await Task.Delay(120);
        _layerHasFocus = false;
        _layerSuggestions.Clear();
    }

    private void OnLayerKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Escape")
        {
            _layerHasFocus = false;
            _layerSuggestions.Clear();
        }
    }

    private Task OnLayerInputAsync() => DebouncedLoadLayerSuggestionsAsync();

    private async Task DebouncedLoadLayerSuggestionsAsync()
    {
        CancelAndDispose(ref _layerCts);
        _layerCts = new CancellationTokenSource();
        var ct = _layerCts.Token;

        try
        {
            await Task.Delay(SuggestDebounce, ct);

            var q = _layer?.Trim();
            if (string.IsNullOrWhiteSpace(q) || q.Length < SuggestMinChars)
            {
                _layerSuggestions.Clear();
                return;
            }

            var items = await LookupService.GetLayerSuggestionsAsync(q, 8, ct);
            if (ct.IsCancellationRequested)
                return;

            _layerSuggestions.Clear();
            _layerSuggestions.AddRange(items);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SelectLayerSuggestion(LayerRmSuggestionDto value)
    {
        _layer = value.Layer;
        _rmRaw = value.RmRaw;
        _layerSuggestions.Clear();
        _layerHasFocus = false;
    }

    private void OnDistrictFocus() => _districtHasFocus = true;

    private async Task OnDistrictBlurAsync()
    {
        await Task.Delay(120);
        _districtHasFocus = false;
        _districtSuggestions.Clear();
    }

    private void OnDistrictKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Escape")
        {
            _districtHasFocus = false;
            _districtSuggestions.Clear();
        }
    }

    private Task OnDistrictInputAsync() => DebouncedLoadDistrictSuggestionsAsync();

    private async Task DebouncedLoadDistrictSuggestionsAsync()
    {
        CancelAndDispose(ref _districtCts);
        _districtCts = new CancellationTokenSource();
        var ct = _districtCts.Token;

        try
        {
            await Task.Delay(SuggestDebounce, ct);

            var q = _districtRaw?.Trim();
            if (string.IsNullOrWhiteSpace(q) || q.Length < SuggestMinChars)
            {
                _districtSuggestions.Clear();
                return;
            }

            var items = await LookupService.GetDistrictSuggestionsAsync(q, 8, ct);
            if (ct.IsCancellationRequested)
                return;

            _districtSuggestions.Clear();
            _districtSuggestions.AddRange(items);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SelectDistrictSuggestion(string value)
    {
        _districtRaw = value;
        _districtSuggestions.Clear();
        _districtHasFocus = false;
    }

    private void OnParticipantFocus(int index)
    {
        _participantSuggestIndex = index;
    }

    private async Task OnParticipantBlurAsync()
    {
        await Task.Delay(120);
        _participantSuggestIndex = null;
        _participantSuggestions.Clear();
    }

    private void OnParticipantKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Escape")
        {
            _participantSuggestIndex = null;
            _participantSuggestions.Clear();
        }
    }

    private bool ShowParticipantSuggestions(int index)
        => _participantSuggestIndex == index && _participantSuggestions.Count > 0;

    private Task OnParticipantInputAsync(int index) => DebouncedLoadParticipantSuggestionsAsync(index);

    private async Task DebouncedLoadParticipantSuggestionsAsync(int index)
    {
        if ((uint)index >= (uint)_participants.Count)
            return;

        var participant = _participants[index];
        participant.SelectedKey = null;
        participant.StatusText = null;

        CancelAndDispose(ref _participantCts);
        _participantCts = new CancellationTokenSource();
        var ct = _participantCts.Token;

        try
        {
            await Task.Delay(SuggestDebounce, ct);

            if (participant.IsUnknown)
            {
                _participantSuggestions.Clear();
                await ReloadActionContextAsync();
                return;
            }

            var q = participant.LabelRaw?.Trim();
            if (string.IsNullOrWhiteSpace(q) || q.Length < SuggestMinChars)
            {
                _participantSuggestions.Clear();
                await ReloadActionContextAsync();
                return;
            }

            var items = await LookupService.SearchParticipantSuggestionsAsync(q, 8, ct);
            if (ct.IsCancellationRequested)
                return;

            _participantSuggestIndex = index;
            _participantSuggestions.Clear();
            _participantSuggestions.AddRange(items);
            await ReloadActionContextAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task OnActionInputAsync()
    {
        await ReloadActionContextAsync();
    }

    private void SelectParticipantSuggestion(int index, ParticipantSuggestionDto value)
    {
        if ((uint)index >= (uint)_participants.Count)
            return;

        var participant = _participants[index];
        participant.LabelRaw = value.Display;
        participant.RoleRaw = value.PrimaryRole;
        participant.SelectedKey = value.Key;
        participant.StatusText = value.Kind switch
        {
            "actor" => "Відома особа",
            "cluster" => "Unknown cluster",
            _ => "Раніше фіксувалась"
        };

        _participantSuggestions.Clear();
        _participantSuggestIndex = null;
        _ = ReloadActionContextAsync();
    }

    private async Task ReloadActionContextAsync()
    {
        CancelAndDispose(ref _contextCts);
        _contextCts = new CancellationTokenSource();
        var ct = _contextCts.Token;

        var keys = _participants
            .Select(x => x.SelectedKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(_actionRaw) || keys.Length == 0)
        {
            _contextLoading = false;
            _actionContext.Clear();
            await InvokeAsync(StateHasChanged);
            return;
        }

        _contextLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await Task.Delay(SuggestDebounce, ct);
            var items = await LookupService.GetActionContextAsync(_actionRaw, keys, 8, ct);
            if (ct.IsCancellationRequested)
                return;

            _actionContext.Clear();
            _actionContext.AddRange(items);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _contextLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private string GetContextBadgeClass(string relationType) => relationType switch
    {
        "direct" => "text-bg-success",
        "same-action" => "text-bg-warning",
        _ => "text-bg-secondary"
    };

    private async Task SaveAsync()
    {
        _error = null;
        _duplicate = false;

        if (string.IsNullOrWhiteSpace(_actionRaw))
        {
            _error = "Поле 'Дія' є обов'язковим.";
            return;
        }

        _saving = true;

        try
        {
            var req = new ObservationCreateRequestDto(
                ObservedDate: DateOnly.FromDateTime(_date),
                DayPart: _dayPart,
                ActionRaw: _actionRaw.Trim(),
                Layer: string.IsNullOrWhiteSpace(_layer) ? null : _layer.Trim(),
                RmRaw: string.IsNullOrWhiteSpace(_rmRaw) ? null : _rmRaw.Trim(),
                PointRaw: string.IsNullOrWhiteSpace(_pointRaw) ? null : _pointRaw.Trim(),
                LocationRaw: string.IsNullOrWhiteSpace(_locationRaw) ? null : _locationRaw.Trim(),
                DistrictRaw: string.IsNullOrWhiteSpace(_districtRaw) ? null : _districtRaw.Trim(),
                Note: string.IsNullOrWhiteSpace(_note) ? null : _note.Trim(),
                Participants: _participants.Select(p => new ObservationCreateParticipantDto(
                    p.LabelRaw,
                    p.IsUnknown,
                    p.RoleRaw)).ToList());

            var result = await WriteService.CreateAsync(req, CancellationToken.None);

            if (result.IsDuplicate)
            {
                _duplicate = true;
                return;
            }

            Navigation.NavigateTo("/observations");
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private Task CancelAsync()
    {
        Navigation.NavigateTo("/observations");
        return Task.CompletedTask;
    }
}
