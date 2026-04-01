//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Toasts;
using Interception.UI.Application.Import.Abstractions;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Interception.UI.Components.Pages.Registry.InterceptionActions;

public partial class ActionCatalog : ComponentBase
{
    private const long MaxImportSizeBytes = 200 * 1024 * 1024; // 200 MB

    [Inject] private IInterceptionActionService ActionService { get; set; } = default!;
    [Inject] private IDatabaseImportExportService DatabaseImportExportService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private IReadOnlyList<InterceptionAction>? _actions;
    private bool _loading;

    // Дравер
    private bool _drawerOpen;
    private InterceptionAction? _editingAction; // null = створення

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    // -------------------------------------------------------------------------
    // Дані
    // -------------------------------------------------------------------------

    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _actions = await ActionService.GetAllAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    // -------------------------------------------------------------------------
    // Відкриття дравера
    // -------------------------------------------------------------------------

    private void OpenCreate()
    {
        _editingAction = null;
        _drawerOpen = true;
    }

    private void OpenEdit(InterceptionAction action)
    {
        _editingAction = action;
        _drawerOpen = true;
    }

    private async Task ExportDatabaseAsync()
    {
        try
        {
            await using var stream = new MemoryStream();
            await DatabaseImportExportService.ExportAsync(stream);

            var bytes = stream.ToArray();
            var fileName = $"interception-db-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

            await Js.InvokeVoidAsync(
                "interception.downloadFromBase64",
                fileName,
                Convert.ToBase64String(bytes),
                "application/json");

            Toasts.Success("Експорт виконано", "Файл backup завантажено.");
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка експорту", ex.Message);
        }
    }

    private async Task ImportDatabaseAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
            return;

        var shouldImport = await Js.InvokeAsync<bool>(
            "confirm",
            "Імпорт DB повністю замінить поточні дані. Продовжити?");

        if (!shouldImport)
            return;

        try
        {
            await using var stream = file.OpenReadStream(MaxImportSizeBytes);
            await DatabaseImportExportService.ImportAsync(stream);
            await LoadAsync();
            Toasts.Success("Імпорт виконано", "Базу даних успішно відновлено.");
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка імпорту", ex.Message);
        }
    }
}
