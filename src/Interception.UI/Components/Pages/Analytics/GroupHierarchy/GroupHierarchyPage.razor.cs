//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.GroupHierarchy;

/// <summary>
/// Операторська сторінка ієрархії груп.
/// </summary>
public partial class GroupHierarchyPage : ComponentBase
{
    [Inject] private IGroupHierarchyService GroupHierarchyService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected GroupHierarchyMapDto? _map;
    protected GroupHierarchyClusterDto? _selectedCluster;
    protected bool _loading;
    protected bool _reviewOnly;

    protected DateTime? _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
    protected DateTime? _dateTo = ConverterDateTimeExtensions.Now.Date;

    protected string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    protected string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

    protected IReadOnlyList<GroupHierarchyClusterDto> VisibleClusters => BuildVisibleClusters(_map, _reviewOnly);

    protected IReadOnlyList<GroupHierarchyNodeDto> ChildNodes => _selectedCluster is null
        ? []
        : [.. _selectedCluster.Nodes.Where(x => !x.IsRoot).OrderBy(x => x.Depth).ThenBy(x => x.Title)];

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (_dateFrom.HasValue && _dateTo.HasValue && _dateFrom.Value.Date > _dateTo.Value.Date)
            {
                Toasts.Error("Некоректний період", "Дата «від» не може бути пізніше за дату «до».");
                _map = null;
                _selectedCluster = null;
                return;
            }

            DateTime? dateFromUtc = null;
            DateTime? dateToUtc = null;

            if (_dateFrom.HasValue)
                dateFromUtc = ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date);

            if (_dateTo.HasValue)
                dateToUtc = ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1).AddTicks(-1));

            _map = await GroupHierarchyService.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);

            if (_map.Clusters.Count == 0)
            {
                _selectedCluster = null;
                return;
            }

            var visible = BuildVisibleClusters(_map, _reviewOnly);
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
            _map = null;
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

    protected async Task ResetPeriodAsync()
    {
        _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
        _dateTo = ConverterDateTimeExtensions.Now.Date;
        _reviewOnly = false;
        await LoadAsync();
    }

    protected void SelectCluster(GroupHierarchyClusterDto cluster)
        => _selectedCluster = cluster;

    protected string ParentMemberLabel(string childGroupKey)
    {
        if (_selectedCluster is null)
            return "невідомий вузол";

        var edge = _selectedCluster.Edges.LastOrDefault(x => x.ChildGroupKey == childGroupKey);
        return edge is null ? "невідомий вузол" : edge.ViaMemberName;
    }

    protected static string FriendlyAction(string? value)
        => string.IsNullOrWhiteSpace(value) ? "дія не визначена" : value;

    protected static string PrimaryFrequency(GroupHierarchyNodeDto node)
        => node.Frequencies.FirstOrDefault() ?? "—";

    private static List<GroupHierarchyClusterDto> BuildVisibleClusters(
        GroupHierarchyMapDto? map,
        bool reviewOnly)
    {
        if (map is null || map.Clusters.Count == 0)
            return [];

        IEnumerable<GroupHierarchyClusterDto> query = map.Clusters;

        if (reviewOnly)
            query = query.Where(x => x.NeedsReview);

        return [.. query
            .OrderByDescending(x => x.Nodes.Count)
            .ThenByDescending(x => x.Edges.Count)
            .ThenBy(x => x.Root.Title)];
    }
}
