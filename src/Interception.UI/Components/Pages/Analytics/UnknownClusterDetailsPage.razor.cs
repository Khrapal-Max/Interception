//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Enums;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

/// <summary>
/// Сторінка деталей unknown-кластера: учасники, історія, пов'язані особи та аналітичні дії.
/// </summary>
public partial class UnknownClusterDetailsPage : ComponentBase
{
    [Inject] public IAnalyticsUnknownClusterDetailsService ClusterService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public Guid ClusterId { get; set; }

    protected AnalyticsUnknownClusterDetailsPageDto? _dto;
    protected readonly List<AnalyticsUnknownClusterLookupDto> _clusters = [];
    protected ClusterDetailsAction CurrentAction { get; set; } = ClusterDetailsAction.Resolve;

    protected bool _loading;
    protected bool _savingAction;
    protected bool _clusterLoading;

    protected string? _clusterSearch;
    protected Guid? _selectedClusterId;
    protected Guid? _selectedParticipantId;
    protected string? _reason;
    protected string? _actorDisplayName;
    protected string? _actorCallsign;
    protected string? _actorNote;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    protected Task NavigateToClustersAsync()
    {
        Navigation.NavigateTo("/analytics/clusters");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Перемикає поточну аналітичну дію та очищує проміжний стан форми.
    /// </summary>
    protected async Task SetCurrentActionAsync(ClusterDetailsAction action)
    {
        if (CurrentAction == action)
            return;

        CurrentAction = action;
        ResetSelectionState(resetParticipant: action == ClusterDetailsAction.Resolve);

        var term = (_clusterSearch ?? string.Empty).Trim();
        if ((action == ClusterDetailsAction.Merge || action == ClusterDetailsAction.Reassign) && term.Length >= 2)
            await SearchClustersAsync();
    }

    /// <summary>
    /// Обирає учасника для перепризначення та переводить сторінку у відповідний режим.
    /// </summary>
    protected async Task BeginReassignAsync(Guid participantId)
    {
        _selectedParticipantId = participantId;
        await SetCurrentActionAsync(ClusterDetailsAction.Reassign);
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

            _clusters.AddRange(await ClusterService.SearchOtherClustersAsync(ClusterId, term, 10, CancellationToken.None));
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка пошуку кластерів.", ex.Message);
        }
        finally
        {
            _clusterLoading = false;
        }
    }

    protected async Task SaveResolveAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ClusterService.ResolveClusterAsActorAsync(_dto.ClusterId, _actorDisplayName ?? string.Empty, _actorCallsign, _actorNote, CancellationToken.None);
            ToastService.Success("Кластер резолвлено.");
        });
    }

    protected async Task SaveMergeAsync()
    {
        if (_dto is null || _selectedClusterId is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ClusterService.MergeClusterAsync(_dto.ClusterId, _selectedClusterId.Value, _reason, CancellationToken.None);
            ToastService.Success("Кластер об'єднано.");
        });
    }

    protected async Task SaveReassignAsync()
    {
        if (_dto is null || _selectedClusterId is null || _selectedParticipantId is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await ClusterService.ReassignParticipantAsync(_dto.ClusterId, _selectedParticipantId.Value, _selectedClusterId.Value, _reason, CancellationToken.None);
            ToastService.Success("Учасника перепризначено.");
        });
    }

    protected static string GetStatusText(string status)
        => status switch
        {
            "open" => "Активний",
            "resolved" => "Резолвлено",
            "merged" => "Об'єднано",
            "archived" => "Архів",
            _ => status
        };

    protected static string GetStatusBadgeClass(string status)
        => status switch
        {
            "open" => "text-bg-warning",
            "resolved" => "text-bg-success",
            "merged" => "text-bg-secondary",
            "archived" => "text-bg-dark",
            _ => "text-bg-light border"
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
            _dto = await ClusterService.GetPageAsync(ClusterId, CancellationToken.None);

            var term = (_clusterSearch ?? string.Empty).Trim();
            if (CurrentAction is ClusterDetailsAction.Merge or ClusterDetailsAction.Reassign && term.Length >= 2)
                await SearchClustersAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження кластера.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ClearActionInputs()
    {
        _reason = null;
        _clusterSearch = null;
        _selectedClusterId = null;
        _selectedParticipantId = null;
        _actorDisplayName = null;
        _actorCallsign = null;
        _actorNote = null;
        _clusters.Clear();
    }

    private void ResetSelectionState(bool resetParticipant)
    {
        _selectedClusterId = null;
        _clusters.Clear();

        if (resetParticipant)
            _selectedParticipantId = null;
    }
}