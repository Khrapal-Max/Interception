//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Candidates;
using Interception.UI.Application.Interceptions.Models.Candidates;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.LinkMap;

/// <summary>
/// Загальна сторінка аналітичної карти зв'язків.
/// </summary>
public partial class LinkMapPage : ComponentBase
{
    [Inject] private ILinkMapService LinkMapService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private LinkMapModel? _map;
    private bool _loading;

    private DateTime? _dateFrom = DateTime.Today.AddDays(-6);
    private DateTime? _dateTo = DateTime.Today;

    private bool _drawerOpen;
    private LinkMapGroupModel? _selectedGroup;

    private string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    private string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

    private int TotalBridgeCount => _map?.Groups.Sum(x => x.Bridges.Count) ?? 0;

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
                return;
            }

            DateTime? dateFromUtc = null;
            DateTime? dateToUtc = null;

            if (_dateFrom.HasValue)
                dateFromUtc = ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date);

            if (_dateTo.HasValue)
                dateToUtc = ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1).AddTicks(-1));

            _map = await LinkMapService.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OnDateFromChanged(string? value)
        => _dateFrom = ConverterDateTimeExtensions.ParseDate(value);

    private void OnDateToChanged(string? value)
        => _dateTo = ConverterDateTimeExtensions.ParseDate(value);

    private async Task ResetPeriodAsync()
    {
        _dateFrom = DateTime.Today.AddDays(-6);
        _dateTo = DateTime.Today;
        await LoadAsync();
    }

    private void OpenGroup(LinkMapGroupModel group)
    {
        _selectedGroup = group;
        _drawerOpen = true;
    }

    private static int GetInternalConnectionWeight(LinkMapGroupModel group)
        => TryReadInt(group, "InternalConnectionWeight") ?? 0;

    private static int GetBridgeWeight(LinkMapGroupModel group)
        => TryReadInt(group, "BridgeWeight") ?? group.Bridges.Sum(x => x.Weight);

    private static int? TryReadInt(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var value = property.GetValue(source);
        return value is int intValue ? intValue : null;
    }
}
