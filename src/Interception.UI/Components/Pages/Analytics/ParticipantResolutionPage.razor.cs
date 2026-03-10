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
/// Окрема аналітична сторінка для резолюції raw-учасника observation.
/// Не викликається з modal/drawer-ланцюга оператора, а працює як самостійна page.
/// </summary>
public partial class ParticipantResolutionPage : ComponentBase
{
    [Inject] public IAnalyticsParticipantResolutionService AnalyticsService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public Guid ParticipantId { get; set; }

    protected AnalyticsParticipantResolutionPageDto? _dto;
    protected readonly List<AnalyticsUnknownClusterLookupDto> _clusters = [];
    protected ResolutionAction CurrentAction { get; set; } = ResolutionAction.CreateCluster;

    protected bool _loading;
    protected bool _savingAction;
    protected bool _clusterLoading;

    protected string? _newClusterDisplayName;
    protected string? _reason;
    protected string? _clusterSearch;
    protected Guid? _selectedClusterId;
    protected string? _actorDisplayName;
    protected string? _actorCallsign;
    protected string? _actorNote;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    protected Task NavigateToObservationAsync()
    {
        Navigation.NavigateTo("/observations");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Перемикає поточну дію аналітика та очищує проміжний стан форми.
    /// </summary>
    protected async Task SetCurrentActionAsync(ResolutionAction action)
    {
        if (CurrentAction == action)
            return;

        CurrentAction = action;
        _selectedClusterId = null;
        _clusters.Clear();

        var term = (_clusterSearch ?? string.Empty).Trim();
        if (action == ResolutionAction.AddToCluster && term.Length >= 2)
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

            _clusters.AddRange(await AnalyticsService.SearchUnknownClustersAsync(term, 8, CancellationToken.None));
        }
        catch (Exception ex)
        {
            ToastService.Error(ex.Message);
        }
        finally
        {
            _clusterLoading = false;
        }
    }

    protected async Task SaveCreateClusterAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await AnalyticsService.CreateUnknownClusterAsync(_dto.ParticipantId, _newClusterDisplayName, _reason, CancellationToken.None);
            ToastService.Success("Гіпотезу створено.");
        });
    }

    protected async Task SaveAddToClusterAsync()
    {
        if (_dto is null || _selectedClusterId is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await AnalyticsService.AddToUnknownClusterAsync(_dto.ParticipantId, _selectedClusterId.Value, _reason, CancellationToken.None);
            ToastService.Success("Учасника додано до наявної гіпотези.");
        });
    }

    protected async Task SaveResolveActorAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await AnalyticsService.ResolveAsActorAsync(_dto.ParticipantId, _actorDisplayName ?? string.Empty, _actorCallsign, _actorNote, CancellationToken.None);
            ToastService.Success("Учасника прив'язано до встановленої особи.");
        });
    }

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
            ToastService.Error(ex.Message);
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
            _dto = await AnalyticsService.GetPageAsync(ParticipantId, CancellationToken.None);

            var term = (_clusterSearch ?? string.Empty).Trim();
            if (CurrentAction == ResolutionAction.AddToCluster && term.Length >= 2)
                await SearchClustersAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ClearActionInputs()
    {
        _newClusterDisplayName = null;
        _reason = null;
        _clusterSearch = null;
        _selectedClusterId = null;
        _actorDisplayName = null;
        _actorCallsign = null;
        _actorNote = null;
        _clusters.Clear();
    }
}