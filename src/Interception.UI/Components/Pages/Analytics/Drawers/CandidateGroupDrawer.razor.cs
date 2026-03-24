//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.Drawers;

public partial class CandidateGroupDrawer : ComponentBase
{
    [Inject] private IResolvedParticipantService ResolvedService { get; set; } = default!;
    [Inject] private IPatternRecognitionService PatternService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public CandidateGroupDto? Group { get; set; }
    [Parameter] public EventCallback OnConfirmed { get; set; }
    [Parameter] public EventCallback OnDismissed { get; set; }

    private string? _confirmName;
    private string? _confirmRole;
    private string? _confirmDivision;
    private bool _nameError;
    private string? _serverError;
    private bool _saving;
    private bool _initialized;

    private IReadOnlyList<KnownParticipantSuggestionDto> _suggestions = [];
    private IReadOnlyList<CandidateContextSuggestionDto> _contextSuggestions = [];
    private bool _loadingSuggestions;

    private string DrawerTitle => Group?.Status switch
    {
        CandidateGroupStatus.Confirmed =>
            $"Підтверджено · {Group.SuggestedName}",
        CandidateGroupStatus.Dismissed =>
            "Відхилена група",
        _ => Group is null ? "Група"
            : $"Група · {Group.ConfidenceScore * 100:F0}% впевненість"
    };

    /// <summary>
    /// Показуємо не кожне сире спостереження окремим рядком, а унікальні
    /// комбінації ознак (частота / вектор / р/м) з кількістю повторів.
    /// Це прибирає шум у дравері коли група має багато однотипних спостережень.
    /// </summary>
    private IReadOnlyList<CandidateGroupFeatureRow> FeatureRows => Group?.Refs is null
        ? []
        : Group.Refs
            .GroupBy(r => new
            {
                Frequency = NormalizeKey(r.Frequency),
                VectorSignal = NormalizeKey(r.VectorSignal),
                Division = NormalizeKey(r.Division)
            })
            .Select(g => new CandidateGroupFeatureRow
            {
                Frequency = FirstNonEmpty(g.Select(x => x.Frequency)),
                VectorSignal = FirstNonEmpty(g.Select(x => x.VectorSignal)),
                Division = FirstNonEmpty(g.Select(x => x.Division)),
                Count = g.Count(),
                FirstObservedDate = g.Min(x => x.ObservedDate),
                LastObservedDate = g.Max(x => x.ObservedDate)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.FirstObservedDate)
            .ToList();

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen) { _initialized = false; return; }
        if (_initialized) return;
        _initialized = true;

        _confirmName = Group?.SuggestedName ?? string.Empty;
        _confirmRole = Group?.SuggestedRole ?? string.Empty;
        _confirmDivision = Group?.SuggestedDivision ?? string.Empty;
        _nameError = false;
        _serverError = null;
        _suggestions = [];
        _contextSuggestions = [];

        // Завантажуємо підказки асинхронно тільки для Open груп
        if (Group?.Status == CandidateGroupStatus.Open)
            await LoadSuggestionsAsync();
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _serverError = null;
        _nameError = false;
        _suggestions = [];
        _contextSuggestions = [];
    }

    private async Task LoadSuggestionsAsync()
    {
        if (Group is null) return;

        _loadingSuggestions = true;
        StateHasChanged();
        try
        {
            _suggestions = await PatternService.GetKnownSuggestionsAsync(Group.Id);
            _contextSuggestions = await PatternService.GetContextSuggestionsAsync(Group.Id);
        }
        catch
        {
            _suggestions = [];
            _contextSuggestions = [];
        }
        finally
        {
            _loadingSuggestions = false;
            StateHasChanged();
        }
    }

    /// <summary>Клік на підказку — заповнює форму підтвердження.</summary>
    private void ApplySuggestion(KnownParticipantSuggestionDto suggestion)
    {
        _confirmName = suggestion.Name;
        _confirmRole = suggestion.Role ?? string.Empty;
        _confirmDivision = suggestion.Division ?? _confirmDivision ?? string.Empty;
        _nameError = false;
    }

    /// <summary>Клік на контекст — не підтверджує особу, а лише підставляє підрозділ.</summary>
    private void ApplyContextSuggestion(CandidateContextSuggestionDto suggestion)
    {
        _confirmDivision = suggestion.Division;
    }

    private async Task ConfirmAsync()
    {
        if (Group is null) return;

        if (string.IsNullOrWhiteSpace(_confirmName))
        {
            _nameError = true;
            return;
        }

        _saving = true;
        _serverError = null;
        try
        {
            await ResolvedService.ConfirmGroupAsync(
                Group.Id,
                new ConfirmCandidateGroupDto
                {
                    Name = _confirmName,
                    Role = _confirmRole,
                    Division = _confirmDivision
                },
                "operator");

            Toasts.Success("Підтверджено",
                $"НВ ідентифіковано як «{_confirmName}».");

            await CloseAsync();
            await OnConfirmed.InvokeAsync();
        }
        catch (InvalidOperationException ex)
        {
            _serverError = ex.Message;
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DismissAsync()
    {
        if (Group is null) return;

        _saving = true;
        try
        {
            await ResolvedService.DismissGroupAsync(Group.Id, "operator");
            Toasts.Warning("Відхилено", "Групу кандидатів відхилено.");
            await CloseAsync();
            await OnDismissed.InvokeAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);

    private static string ScoreClass(double score) => score switch
    {
        >= 0.75 => "bg-success",
        >= 0.50 => "bg-warning",
        _ => "bg-danger"
    };

    private static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    private static string? FirstNonEmpty(IEnumerable<string?> values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private sealed class CandidateGroupFeatureRow
    {
        public string? Frequency { get; init; }
        public string? VectorSignal { get; init; }
        public string? Division { get; init; }
        public int Count { get; init; }
        public DateTime FirstObservedDate { get; init; }
        public DateTime LastObservedDate { get; init; }
    }
}
