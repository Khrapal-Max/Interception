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
    }

    private async Task LoadSuggestionsAsync()
    {
        if (Group is null) return;

        _loadingSuggestions = true;
        StateHasChanged();
        try
        {
            _suggestions = await PatternService.GetKnownSuggestionsAsync(Group.Id);
        }
        catch
        {
            _suggestions = [];
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
        _nameError = false;
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
}
