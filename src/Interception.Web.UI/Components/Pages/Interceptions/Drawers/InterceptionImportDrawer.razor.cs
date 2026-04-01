//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Import.Abstractions;
using Interception.Application.Import.Dtos;
using Interception.Application.Toasts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Interception.Web.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionImportDrawer : ComponentBase
{
    [Inject] private IInterceptionImportService ImportService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Parameters
    // -------------------------------------------------------------------------

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback OnImported { get; set; }

    // -------------------------------------------------------------------------
    // Стан
    // -------------------------------------------------------------------------

    private IBrowserFile? _file;
    private string? _fileName;
    private ImportResultDto? _lastResult;
    private bool _importing;

    // -------------------------------------------------------------------------
    // Скидання стану при закритті
    // -------------------------------------------------------------------------

    private void OnDrawerClosed()
    {
        _file = null;
        _fileName = null;
        _lastResult = null;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _file = e.File;
        _fileName = e.File.Name;
        _lastResult = null;
    }

    private async Task RunImportAsync()
    {
        if (_file is null) return;

        _importing = true;
        try
        {
            await using var stream = _file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            _lastResult = await ImportService.ImportAsync(stream, "operator");

            if (_lastResult.ImportedCount > 0)
            {
                Toasts.Success(
                    "Імпорт завершено",
                    $"Імпортовано: {_lastResult.ImportedCount} | Пропущено: {_lastResult.SkippedCount}");

                await CloseAsync();
                await OnImported.InvokeAsync();
            }
            else
            {
                Toasts.Warning(
                    "Імпорт не вдався",
                    $"Жоден рядок не імпортовано. Помилок: {_lastResult.SkippedCount}");
            }
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка імпорту", ex.Message);
        }
        finally
        {
            _importing = false;
        }
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
