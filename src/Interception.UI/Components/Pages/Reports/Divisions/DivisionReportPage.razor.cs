//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Reports;
using Interception.UI.Application.Interceptions.Models.Reports;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Reports.Divisions;

/// <summary>
/// Сторінка зведеного звіту по підрозділах.
/// </summary>
public partial class DivisionReportPage : ComponentBase
{
    [Inject] private IDivisionReportService DivisionReportService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private DivisionReportModel? _report;
    private bool _loading;

    private DateTime? _dateFrom = DateTime.Today.AddDays(-6);
    private DateTime? _dateTo = DateTime.Today;

    private string? DateFromStr => ConverterDateTimeExtensions.FormatDate(_dateFrom);
    private string? DateToStr => ConverterDateTimeExtensions.FormatDate(_dateTo);

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
                _report = null;
                return;
            }

            DateTime? dateFromUtc = null;
            DateTime? dateToUtcExclusive = null;

            if (_dateFrom.HasValue)
                dateFromUtc = ConverterDateTimeExtensions.ToUtc(_dateFrom.Value.Date);

            if (_dateTo.HasValue)
                dateToUtcExclusive = ConverterDateTimeExtensions.ToUtc(_dateTo.Value.Date.AddDays(1));

            _report = await DivisionReportService.BuildAsync(
                dateFromUtc,
                dateToUtcExclusive,
                CancellationToken.None);
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
}