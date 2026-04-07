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
/// Компактна кнопка експорту без меню.
/// Один компонент = один тип експорту.
/// </summary>
public partial class ExportLauncher : ComponentBase
{
    [Inject] private IExcelExportService ExportService { get; set; } = default!;
    [Inject] private IPdfExportService PdfExportService { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    /// <summary>
    /// Явний вид експорту.
    /// </summary>
    [Parameter] public ExportKind? Kind { get; set; }

    /// <summary>
    /// Формат файла експорту.
    /// </summary>
    [Parameter] public ExportFileFormat Format { get; set; } = ExportFileFormat.Excel;

    /// <summary>
    /// Текст кнопки.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// CSS-клас кнопки.
    /// </summary>
    [Parameter] public string ButtonClass { get; set; } = "btn btn-outline-light btn-sm rounded-0";

    [Parameter] public DateTime? DateFrom { get; set; }
    [Parameter] public DateTime? DateTo { get; set; }
    [Parameter] public DateTime? Day { get; set; }

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
    private string _resolvedTitle = "Експорт";

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
            ? BuildDefaultTitle(_resolvedKind, Format)
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

    private static string BuildDefaultTitle(ExportKind? kind, ExportFileFormat format)
    {
        var suffix = format == ExportFileFormat.Pdf ? " (PDF)" : string.Empty;

        return kind switch
        {
            ExportKind.Interceptions => "Експорт спостережень" + suffix,
            ExportKind.FrequencyDivisionRegistry => "Експорт частота / підрозділ" + suffix,
            ExportKind.PersonsRegistry => "Експорт осіб" + suffix,
            ExportKind.DivisionReport => "Експорт звіту" + suffix,
            ExportKind.DayPicture => "Експорт картини дня" + suffix,
            ExportKind.LinkMap => "Експорт карти зв'язків" + suffix,
            ExportKind.FrequencyWeights => "Експорт ваг підрозділів" + suffix,
            ExportKind.GroupHierarchy => "Експорт ієрархії груп" + suffix,
            _ => "Експорт"
        };
    }

    private async Task ExportAsync()
    {
        if (_busy || !_resolvedKind.HasValue)
            return;

        _busy = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var request = new ExportRequestDto(_resolvedKind.Value, DateFrom, DateTo, Day);
            var file = Format == ExportFileFormat.Pdf
                ? await PdfExportService.ExportAsync(request, CancellationToken.None)
                : await ExportService.ExportAsync(request, CancellationToken.None);

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
