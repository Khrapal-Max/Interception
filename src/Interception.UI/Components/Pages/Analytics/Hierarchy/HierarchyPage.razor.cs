//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.Hierarchy;

/// <summary>
/// Сторінка ієрархії груп.
/// Показує опорні групи, дочірні контури та вузли переходу між ними.
/// </summary>
public partial class HierarchyPage : ComponentBase
{
    [Inject] private IGroupHierarchyService GroupHierarchyService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "group")]
    public string? RequestedGroupKey { get; set; }

    protected GroupHierarchyDto? _hierarchy;
    protected bool _loading;

    protected DateTime? _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
    protected DateTime? _dateTo = ConverterDateTimeExtensions.Now.Date;

    protected string? _search;
    protected bool _onlyNested = true;

    protected GroupHierarchyClusterDto? _selectedCluster;
    protected GroupHierarchyClusterDto? _drawerCluster;
    protected bool _drawerOpen;

    protected string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    protected string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

    protected IReadOnlyList<GroupHierarchyClusterDto> VisibleClusters => BuildVisibleClusters(_hierarchy, _search, _onlyNested);

    protected IReadOnlyList<GroupHierarchyNodeDto> ChildNodes => _selectedCluster?.Nodes.Where(x => !x.IsRoot).ToList() ?? [];

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    protected override Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(RequestedGroupKey) && _hierarchy is not null)
        {
            _selectedCluster = FindRequestedCluster(_hierarchy, RequestedGroupKey) ?? _selectedCluster;
        }

        return Task.CompletedTask;
    }

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (_dateFrom.HasValue && _dateTo.HasValue && _dateFrom.Value.Date > _dateTo.Value.Date)
            {
                Toasts.Error("Некоректний період", "Дата «від» не може бути пізніше за дату «до».");
                _hierarchy = null;
                _selectedCluster = null;
                return;
            }

            DateTime? dateFromUtc = null;
            DateTime? dateToUtc = null;

            if (_dateFrom.HasValue)
                dateFromUtc = ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date);

            if (_dateTo.HasValue)
                dateToUtc = ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1).AddTicks(-1));

            _hierarchy = await GroupHierarchyService.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);
            var visible = BuildVisibleClusters(_hierarchy, _search, _onlyNested);

            var requested = FindRequestedCluster(_hierarchy, RequestedGroupKey);
            if (requested is not null)
            {
                _selectedCluster = requested;
                return;
            }

            if (_selectedCluster is null)
            {
                _selectedCluster = visible.FirstOrDefault();
                return;
            }

            _selectedCluster = visible.FirstOrDefault(x => x.ClusterKey == _selectedCluster.ClusterKey)
                               ?? visible.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _hierarchy = null;
            _selectedCluster = null;
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void OnDateFromChanged(string? value)
        => _dateFrom = ConverterDateTimeExtensions.ParseDate(value);

    protected void OnDateToChanged(string? value)
        => _dateTo = ConverterDateTimeExtensions.ParseDate(value);

    protected void OnSearchChanged(string? value)
    {
        _search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (_selectedCluster is not null && !VisibleClusters.Any(x => x.ClusterKey == _selectedCluster.ClusterKey))
            _selectedCluster = VisibleClusters.FirstOrDefault();
    }

    protected async Task ResetPeriodAsync()
    {
        _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
        _dateTo = ConverterDateTimeExtensions.Now.Date;
        _search = null;
        _onlyNested = true;
        await LoadAsync();
    }

    protected void SelectCluster(GroupHierarchyClusterDto cluster)
        => _selectedCluster = cluster;

    protected void OpenClusterDrawer(GroupHierarchyClusterDto cluster)
    {
        _drawerCluster = cluster;
        _drawerOpen = true;
    }

    protected Task CloseDrawerAsync()
    {
        _drawerOpen = false;
        _drawerCluster = null;
        return Task.CompletedTask;
    }

    protected static string FriendlyAction(string? value)
        => string.IsNullOrWhiteSpace(value) ? "дія не визначена" : value;

    protected static string FriendlyDivision(string? value)
        => string.IsNullOrWhiteSpace(value) ? "НВ підрозділ" : value;

    protected static string BuildSummary(GroupHierarchyClusterDto cluster)
    {
        if (cluster.TransitionCount == 0)
            return $"Група {cluster.RootCenterName} зараз виглядає як самостійний контур без дочірніх груп.";

        return $"Група {cluster.RootCenterName} є опорною і тримає {cluster.TransitionCount} дочірн. контур(и).";
    }

    private static GroupHierarchyClusterDto? FindRequestedCluster(GroupHierarchyDto? hierarchy, string? groupKey)
    {
        if (hierarchy is null || string.IsNullOrWhiteSpace(groupKey))
            return null;

        return hierarchy.Clusters.FirstOrDefault(x =>
            string.Equals(x.RootGroupKey, groupKey, StringComparison.OrdinalIgnoreCase)
            || x.Nodes.Any(n => string.Equals(n.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<GroupHierarchyClusterDto> BuildVisibleClusters(
        GroupHierarchyDto? hierarchy,
        string? search,
        bool onlyNested)
    {
        if (hierarchy is null || hierarchy.Clusters.Count == 0)
            return [];

        IEnumerable<GroupHierarchyClusterDto> query = hierarchy.Clusters;

        if (onlyNested)
            query = query.Where(x => x.TransitionCount > 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.RootCenterName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Division) && x.Division.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.PrimaryAction) && x.PrimaryAction.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.TopActions.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.Nodes.Any(n => n.CenterName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. query
            .OrderByDescending(x => x.TransitionCount)
            .ThenByDescending(x => x.TotalGroups)
            .ThenByDescending(x => x.TotalUniqueMembers)
            .ThenBy(x => x.RootCenterName, StringComparer.OrdinalIgnoreCase)];
    }
}
