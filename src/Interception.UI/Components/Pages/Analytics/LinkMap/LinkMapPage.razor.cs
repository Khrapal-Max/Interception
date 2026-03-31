//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Models;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.LinkMap;

/// <summary>
/// Сторінка карти зв'язків у режимі:
/// список груп → вибір групи → фокус на її ролі, діях і взаємодіях.
/// </summary>
public partial class LinkMapPage : ComponentBase
{
    [Inject] private ILinkMapService LinkMapService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected LinkMapModel? _map;
    protected bool _loading;

    protected DateTime? _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
    protected DateTime? _dateTo = ConverterDateTimeExtensions.Now.Date;

    protected string? _search;
    protected string _sort = "connections";
    protected bool _strongOnly;

    protected LinkMapGroupModel? _selectedGroup;
    protected LinkMapGroupModel? _drawerGroup;
    protected bool _drawerOpen;

    protected string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    protected string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

    protected IReadOnlyList<LinkMapGroupModel> VisibleGroups => BuildVisibleGroups(_map, _search, _sort, _strongOnly);

    internal IReadOnlyList<FocusedLinkModel> FocusedLinks => BuildFocusedLinks(_selectedGroup, _map);

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
                _selectedGroup = null;
                return;
            }

            DateTime? dateFromUtc = null;
            DateTime? dateToUtc = null;

            if (_dateFrom.HasValue)
                dateFromUtc = ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date);

            if (_dateTo.HasValue)
                dateToUtc = ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1).AddTicks(-1));

            _map = await LinkMapService.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);

            if (_map.Groups.Count == 0)
            {
                _selectedGroup = null;
                return;
            }

            var visible = BuildVisibleGroups(_map, _search, _sort, _strongOnly);
            if (_selectedGroup is null)
            {
                _selectedGroup = visible.FirstOrDefault();
                return;
            }

            _selectedGroup = _map.Groups.FirstOrDefault(x => x.GroupKey == _selectedGroup.GroupKey)
                             ?? visible.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _map = null;
            _selectedGroup = null;
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

        if (_selectedGroup is not null && !VisibleGroups.Any(x => x.GroupKey == _selectedGroup.GroupKey))
            _selectedGroup = VisibleGroups.FirstOrDefault();
    }

    protected async Task ResetPeriodAsync()
    {
        _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
        _dateTo = ConverterDateTimeExtensions.Now.Date;
        _search = null;
        _sort = "connections";
        _strongOnly = false;
        await LoadAsync();
    }

    protected void SelectGroup(LinkMapGroupModel group)
        => _selectedGroup = group;

    protected void OpenSelectedGroupDrawer()
    {
        if (_selectedGroup is null)
            return;

        _drawerGroup = _selectedGroup;
        _drawerOpen = true;
    }

    protected Task CloseDrawerAsync()
    {
        _drawerOpen = false;
        _drawerGroup = null;
        return Task.CompletedTask;
    }

    private static List<LinkMapGroupModel> BuildVisibleGroups(
        LinkMapModel? map,
        string? search,
        string sort,
        bool strongOnly)
    {
        if (map is null || map.Groups.Count == 0)
            return [];

        IEnumerable<LinkMapGroupModel> query = map.Groups;

        if (strongOnly)
            query = query.Where(x => x.Bridges.Count > 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.KeyPersonName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Division) && x.Division.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.PrimaryAction) && x.PrimaryAction.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.TopActions.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.Frequencies.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        query = sort switch
        {
            "action" => query
                .OrderBy(x => string.IsNullOrWhiteSpace(x.PrimaryAction) ? 1 : 0)
                .ThenBy(x => x.PrimaryAction)
                .ThenBy(x => x.KeyPersonName),
            "name" => query
                .OrderBy(x => x.KeyPersonName)
                .ThenBy(x => x.Division),
            _ => query
                .OrderByDescending(x => x.Bridges.Count)
                .ThenByDescending(x => x.BridgeWeight)
                .ThenByDescending(x => x.Members.Count)
                .ThenBy(x => x.KeyPersonName)
        };

        return [.. query];
    }

    private static List<FocusedLinkModel> BuildFocusedLinks(LinkMapGroupModel? selectedGroup, LinkMapModel? map)
    {
        if (selectedGroup is null || map is null)
            return [];

        var groupsByKey = map.Groups.ToDictionary(x => x.GroupKey, StringComparer.OrdinalIgnoreCase);

        return [.. selectedGroup.Bridges
            .Where(x => groupsByKey.ContainsKey(x.TargetGroupKey))
            .Select(x => new FocusedLinkModel(groupsByKey[x.TargetGroupKey], x))
            .OrderByDescending(x => x.Bridge.Weight)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Bridge.PrimaryAction))
            .ThenBy(x => x.TargetGroup.KeyPersonName)];
    }
}
