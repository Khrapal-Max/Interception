//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Import;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Interception.UI.Components.Pages.Observations;

public partial class ObservationImportPanel : ComponentBase
{
    [Inject] public ObservationImportService ImportService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public EventCallback OnImported { get; set; }

    private IBrowserFile? _selectedFile;
    private bool _importing;
    private ObservationImportResult? _result;

    private int _firstRowNumber = 2;

    // NOTE: Make it async to ensure UI re-renders reliably after selecting a file
    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        _result = null;

        // If user picks a file while import was running, unlock UI.
        _importing = false;

        _selectedFile = e.File;

        // Force UI refresh (some hosting modes may not re-render on InputFile change)
        await InvokeAsync(StateHasChanged);
    }


    private bool ValidateSelectedFile()
    {
        if (_selectedFile is null) return false;

        // BrowserFile.ContentType may be empty; rely on extension.
        var name = _selectedFile.Name ?? string.Empty;
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ToastService.Warning("Потрібен файл Excel формату .xlsx");
            return false;
        }

        return true;
    }

    private async Task ImportAsync()
    {
        if (_selectedFile is null) return;

        if (!ValidateSelectedFile())
        {
            _importing = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        _result = null;
        _importing = true;

        try
        {
            // 20 MB max (adjust if needed)
            await using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);

            var options = new ObservationImportOptions
            {
                Source = "import",
                SourceFileId = Guid.NewGuid(),
                FirstSourceRowNumber = _firstRowNumber,
                DeduplicateByHash = true
            };

            _result = await ImportService.ImportXlsxAsync(stream, options, CancellationToken.None);

            if (OnImported.HasDelegate)
                await OnImported.InvokeAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error(ex.Message);
        }
        finally
        {
            _importing = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
