//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;
using Interception.UI.Application.Analytics.Enums;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

/// <summary>
/// Аналітична сторінка роботи з raw-учасником observation.
/// </summary>
public partial class ParticipantResolutionPage : ComponentBase
{
    [Inject] public IAnalyticsParticipantResolutionService ResolutionService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public Guid ParticipantId { get; set; }

    protected AnalyticsParticipantResolutionPageDto? _dto;
    protected readonly List<AnalyticsUnknownClusterLookupDto> _clusters = [];

    protected bool _loading;
    protected bool _savingAction;
    protected bool _clusterLoading;

    protected ParticipantAction CurrentAction { get; set; } = ParticipantAction.CreateHypothesis;

    protected string? _newHypothesisName;
    protected string? _hypothesisRole;
    protected string? _confirmedRole;
    protected string? _reason;
    protected string? _clusterSearch;
    protected Guid? _selectedClusterId;

    protected string? _actorDisplayName;
    protected string? _actorCallsign;
    protected string? _actorNote;

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    protected async Task SetCurrentActionAsync(ParticipantAction action)
    {
        if (CurrentAction == action)
            return;

        CurrentAction = action;
        _selectedClusterId = null;
        _clusters.Clear();

        var term = (_clusterSearch ?? string.Empty).Trim();
        if (action == ParticipantAction.AddToHypothesis && term.Length >= 2)
            await SearchClustersAsync();
    }

    protected async Task SearchClustersAsync()
    {
        _clusterLoading = true;

        try
        {
            _selectedClusterId = null;
            _clusters.Clear();

            var term = (_clusterSearch ?? string.Empty).Trim();
            if (term.Length < 2)
                return;

            _clusters.AddRange(await ResolutionService.SearchUnknownClustersAsync(term, 10, CancellationToken.None));
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка пошуку припущень.", ex.Message);
        }
        finally
        {
            _clusterLoading = false;
        }
    }

    protected async Task SaveCreateHypothesisAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ResolutionService.CreateUnknownClusterAsync(
                _dto.ParticipantId,
                _newHypothesisName,
                _hypothesisRole,
                _reason,
                CancellationToken.None);

            ToastService.Success("Нове припущення створено.");
        });
    }

    protected async Task SaveAddToHypothesisAsync()
    {
        if (_dto is null || _selectedClusterId is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ResolutionService.AddToUnknownClusterAsync(
                _dto.ParticipantId,
                _selectedClusterId.Value,
                _reason,
                CancellationToken.None);

            ToastService.Success("Учасника додано до припущення.");
        });
    }

    protected async Task SaveResolveFactAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ResolutionService.ResolveAsActorAsync(
                _dto.ParticipantId,
                _actorDisplayName ?? string.Empty,
                _confirmedRole,
                _actorCallsign,
                _actorNote,
                CancellationToken.None);

            ToastService.Success("Факт підтверджено.");
        });
    }

    protected static string GetArchiveReasonText(string? reason)
        => (reason ?? string.Empty).ToLowerInvariant() switch
        {
            "resolved" => "Встановлено особу",
            "merged" => "Об'єднано",
            "empty" => "Порожня після переносу",
            "archived" => "Архівовано",
            _ => "—"
        };

    private async Task ExecuteActionAsync(Func<Task> action)
    {
        _savingAction = true;

        try
        {
            await action();
            ClearActionInputs();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка аналітичної дії.", ex.Message);
        }
        finally
        {
            _savingAction = false;
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _dto = await ResolutionService.GetPageAsync(ParticipantId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження сторінки аналітики.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ClearActionInputs()
    {
        _newHypothesisName = null;
        _hypothesisRole = null;
        _reason = null;
        _clusterSearch = null;
        _selectedClusterId = null;
        _clusters.Clear();

        _actorDisplayName = null;
        _confirmedRole = null;
        _actorCallsign = null;
        _actorNote = null;
    }
}