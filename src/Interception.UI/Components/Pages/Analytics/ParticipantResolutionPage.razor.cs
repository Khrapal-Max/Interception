//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
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

    [Parameter] public Guid ParticipantId { get; set; }

    protected AnalyticsParticipantResolutionPageDto? _dto;
    protected readonly List<AnalyticsUnknownClusterLookupDto> _clusters = [];
    protected ResolutionAction CurrentAction { get; set; } = ResolutionAction.CreateCluster;

    protected bool _loading;
    protected bool _savingAction;
    protected bool _clusterLoading;
    protected string? _error;
    protected string? _message;

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

    protected async Task NavigateToObservationAsync()
    {
        if (_dto is null)
            return;

        Navigation.NavigateTo("/observations");
        await Task.CompletedTask;
    }

    protected async Task SearchClustersAsync()
    {
        _clusterLoading = true;
        _error = null;

        try
        {
            _clusters.Clear();
            _clusters.AddRange(await AnalyticsService.SearchUnknownClustersAsync(_clusterSearch ?? string.Empty, 8, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
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
            _message = "Гипотезу створено.";
        });
    }

    protected async Task SaveAddToClusterAsync()
    {
        if (_dto is null || _selectedClusterId is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await AnalyticsService.AddToUnknownClusterAsync(_dto.ParticipantId, _selectedClusterId.Value, _reason, CancellationToken.None);
            _message = "Учасника додано до existing cluster.";
        });
    }

    protected async Task SaveResolveActorAsync()
    {
        if (_dto is null)
            return;

        await ExecuteActionAsync(async () =>
        {
            await AnalyticsService.ResolveAsActorAsync(_dto.ParticipantId, _actorDisplayName ?? string.Empty, _actorCallsign, _actorNote, CancellationToken.None);
            _message = "Учасника позначено як actor.";
        });
    }

    private async Task ExecuteActionAsync(Func<Task> action)
    {
        _savingAction = true;
        _error = null;
        _message = null;

        try
        {
            await action();
            ClearActionInputs();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _savingAction = false;
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            _dto = await AnalyticsService.GetPageAsync(ParticipantId, CancellationToken.None);
            if (CurrentAction == ResolutionAction.AddToCluster)
                await SearchClustersAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
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
        _selectedClusterId = null;
        _actorDisplayName = null;
        _actorCallsign = null;
        _actorNote = null;
    }

    /// <summary>
    /// Поточна дія аналітика на сторінці резолюції.
    /// </summary>
    protected enum ResolutionAction
    {
        CreateCluster,
        AddToCluster,
        ResolveActor
    }
}
