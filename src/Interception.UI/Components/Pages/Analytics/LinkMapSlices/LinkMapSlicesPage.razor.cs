//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.LinkMapSlices;

/// <summary>
/// Окрема сторінка «зрізів» link-map:
/// група осіб / спільні частоти / спільна група дій / прогалини даних.
/// </summary>
public partial class LinkMapSlicesPage : ComponentBase
{
    [Inject] private ILinkMapSliceService SliceService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected LinkMapSliceDto? _slice;
    protected bool _loading;

    protected DateTime? _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
    protected DateTime? _dateTo = ConverterDateTimeExtensions.Now.Date;

    protected string? _search;
    protected bool _missingOnly;

    protected string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    protected string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

    protected IReadOnlyList<LinkMapGroupSliceDto> VisibleGroups => BuildVisibleGroups(_slice, _search, _missingOnly);

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
                _slice = null;
                return;
            }

            DateTime? dateFromUtc = _dateFrom.HasValue
                ? ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date)
                : null;

            DateTime? dateToUtc = _dateTo.HasValue
                ? ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1).AddTicks(-1))
                : null;

            _slice = await SliceService.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _slice = null;
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
        => _search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected async Task ResetPeriodAsync()
    {
        _dateFrom = ConverterDateTimeExtensions.Now.Date.AddDays(-6);
        _dateTo = ConverterDateTimeExtensions.Now.Date;
        _search = null;
        _missingOnly = false;
        await LoadAsync();
    }

    private static List<LinkMapGroupSliceDto> BuildVisibleGroups(
        LinkMapSliceDto? slice,
        string? search,
        bool missingOnly)
    {
        if (slice is null || slice.Groups.Count == 0)
            return [];

        IEnumerable<LinkMapGroupSliceDto> query = slice.Groups;

        if (missingOnly)
            query = query.Where(x => x.IsDivisionMissing || x.MissingRoleCount > 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.KeyPersonName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Members.Any(m => m.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.SharedActions.Any(a => a.Contains(term, StringComparison.OrdinalIgnoreCase))
                || x.SharedFrequencies.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.Division)
                    && x.Division.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. query];
    }
}
