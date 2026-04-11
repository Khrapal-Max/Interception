//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Exports.Dtos;
using Interception.UI.Application.Exports.Models;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Interception.UI.Components.Shared.Export;

/// <summary>
/// Компактна кнопка Excel-експорту без меню.
/// Один компонент = один тип експорту.
/// </summary>
public partial class ExportLauncher : ComponentBase
{
    [Inject] private IExcelExportService ExportService { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    /// <summary>
    /// Явний вид експорту.
    /// Рекомендований режим використання.
    /// </summary>
    [Parameter] public ExportKind? Kind { get; set; }

    /// <summary>
    /// Текст кнопки.
    /// Якщо не задано, визначається автоматично від виду експорту.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// CSS-клас кнопки.
    /// </summary>
    [Parameter] public string ButtonClass { get; set; } = "btn btn-outline-light btn-sm rounded-0";

    /// <summary>
    /// Початок діапазону для сторінкового експорту.
    /// </summary>
    [Parameter] public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Кінець діапазону для сторінкового експорту.
    /// </summary>
    [Parameter] public DateTime? DateTo { get; set; }

    /// <summary>
    /// День для експорту картини дня.
    /// </summary>
    [Parameter] public DateTime? Day { get; set; }

    // Legacy flags залишені для сумісності зі старими вставками.
    [Parameter] public bool ShowInterceptions { get; set; }
    [Parameter] public bool ShowFrequencyRegistry { get; set; }
    [Parameter] public bool ShowPersonsRegistry { get; set; }
    [Parameter] public bool ShowDivisionReport { get; set; }
    [Parameter] public bool ShowDayPicture { get; set; }
    [Parameter] public bool ShowLinkMap { get; set; }
    [Parameter] public bool ShowFrequencyWeights { get; set; }

    private bool _busy;
    private bool _hasAction;
    private ExportKind? _resolvedKind;
    private string _resolvedTitle = ".xlsx";

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        ResolveAction();
        return Task.CompletedTask;
    }

    private void ResolveAction()
    {
        _resolvedKind = Kind ?? ResolveLegacyKind();
        _hasAction = _resolvedKind.HasValue;
        _resolvedTitle = string.IsNullOrWhiteSpace(Title)
            ? BuildDefaultTitle(_resolvedKind)
            : Title!.Trim();
    }

    private ExportKind? ResolveLegacyKind()
    {
        if (ShowInterceptions)
            return ExportKind.Interceptions;

        if (ShowFrequencyRegistry)
            return ExportKind.FrequencyDivisionRegistry;

        if (ShowPersonsRegistry)
            return ExportKind.PersonsRegistry;

        if (ShowDivisionReport)
            return ExportKind.DivisionReport;

        if (ShowDayPicture)
            return ExportKind.DayPicture;

        if (ShowLinkMap)
            return ExportKind.LinkMap;

        if (ShowFrequencyWeights)
            return ExportKind.FrequencyWeights;

        return null;
    }

    private static string BuildDefaultTitle(ExportKind? kind)
        => ".xlsx";

    private async Task ExportAsync()
    {
        if (_busy || !_resolvedKind.HasValue)
            return;

        _busy = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var file = await ExportService.ExportAsync(
                new ExportRequestDto(_resolvedKind.Value, DateFrom, DateTo, Day),
                CancellationToken.None);

            await Js.InvokeVoidAsync(
                "interceptionExport.downloadFile",
                file.FileName,
                file.ContentType,
                Convert.ToBase64String(file.Content));

            Toasts.Success("Експорт готовий", file.FileName);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка експорту", ex.Message);
        }
        finally
        {
            _busy = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
