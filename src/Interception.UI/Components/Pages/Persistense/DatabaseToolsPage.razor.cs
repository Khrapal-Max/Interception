//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Database.Abstractions;
using Interception.UI.Application.Database.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Interception.UI.Components.Pages.Persistense;

/// <summary>
/// Сторінка файлових операцій над portable SQLite-базою.
/// </summary>
public partial class DatabaseToolsPage : ComponentBase
{
    [Inject] private IDatabaseMaintenanceService DatabaseService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private DatabaseStatusDto? _status;
    private bool _loading;
    private bool _busy;
    private string? _busyOperation;
    private IBrowserFile? _selectedFile;
    private string? _selectedFileName;
    private string _clearConfirmation = string.Empty;

    private bool CanClear => string.Equals(_clearConfirmation?.Trim(), "ОЧИСТИТИ", StringComparison.Ordinal);

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _status = await DatabaseService.GetStatusAsync(CancellationToken.None);
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

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        _selectedFileName = e.File.Name;
    }

    private async Task RunImportAsync()
    {
        if (_selectedFile is null)
            return;

        _busy = true;
        _busyOperation = "import";

        try
        {
            await using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 1024L * 1024 * 1024);
            var result = await DatabaseService.ImportAsync(stream, _selectedFile.Name, "operator", CancellationToken.None);

            var backupFileName = string.IsNullOrWhiteSpace(result.BackupFilePath)
                ? null
                : Path.GetFileName(result.BackupFilePath);

            var toastMessage = string.IsNullOrWhiteSpace(backupFileName)
                ? result.Message
                : $"{result.Message} Backup: {backupFileName}";

            Toasts.Success(result.Title, toastMessage);

            _selectedFile = null;
            _selectedFileName = null;

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка імпорту бази", ex.Message);
        }
        finally
        {
            _busy = false;
            _busyOperation = null;
        }
    }

    private async Task RunClearAsync()
    {
        if (!CanClear)
            return;

        _busy = true;
        _busyOperation = "clear";

        try
        {
            var result = await DatabaseService.ClearAsync("operator", CancellationToken.None);

            var backupFileName = string.IsNullOrWhiteSpace(result.BackupFilePath)
                ? null
                : Path.GetFileName(result.BackupFilePath);

            var toastMessage = string.IsNullOrWhiteSpace(backupFileName)
                ? result.Message
                : $"{result.Message} Backup: {backupFileName}";

            Toasts.Warning(result.Title, toastMessage);

            _clearConfirmation = string.Empty;
            _selectedFile = null;
            _selectedFileName = null;

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка очищення бази", ex.Message);
        }
        finally
        {
            _busy = false;
            _busyOperation = null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static string FormatDate(DateTime? utc)
        => utc is null ? "—" : ConverterDateTimeExtensions.ToDisplay(utc.Value).ToString("dd.MM.yyyy HH:mm:ss");

    private static string FormatBool(bool value)
        => value ? "так" : "ні";
}
