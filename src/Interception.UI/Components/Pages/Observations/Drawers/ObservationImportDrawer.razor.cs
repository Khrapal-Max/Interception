//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos.Import;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Components.Shared.Drawer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationImportDrawer : ComponentBase, IDisposable
{
    private static readonly string[] TemplateColumns =
    [
        "Час",
        "Дія",
        "Шар",
        "Р/М",
        "Точка",
        "Локація",
        "Район",
        "Підрозділ",
        "Сила",
        "Примітка",
        "Учасники"
    ];

    private static readonly string[][] SampleRows =
    [
        ["2026-03-20 14:25", "Радіообмін", "Шар-А", "РМ-1", "Пост-2", "лісосмуга пн.", "район 1", "70 тп", "2", "короткий обмін", "НВ 1:старший|Орбіта:оператор"],
        ["2026-03-20 14:41", "Переміщення", "Шар-А", "РМ-1", "Пост-2", "дорога на сх.", "район 1", "70 тп", "1", "зміна позиції", "НВ 2:водій"]
    ];

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback Imported { get; set; }

    [Inject] private IObservationImportService ImportService { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    private readonly CancellationTokenSource _lifetimeCts = new();

    private Drawer? _drawer;
    private IBrowserFile? _selectedFile;
    private ObservationImportDrawerModel _model = new();
    private ObservationImportResultDto? _result;
    private bool _importing;

    private static string ParticipantsHint => "Формат учасників: НВ 1:старший|Орбіта:оператор. Також можна розділяти ';' або переносом рядка.";

    private async Task OnFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        _model.SelectedFileName = e.File.Name;
        _model.FileInfo = e.File.Size > 0
            ? $"Файл: {e.File.Name}, розмір: {e.File.Size / 1024d:0.#} KB"
            : $"Файл: {e.File.Name}";
        _result = null;

        await InvokeAsync(StateHasChanged);
    }

    private void ClearInput()
    {
        _selectedFile = null;
        _model = new ObservationImportDrawerModel();
        _result = null;
    }

    private async Task ImportAsync()
    {
        if (_selectedFile is null)
        {
            ToastService.Error("Помилка імпорту.", "Оберіть Excel-файл .xlsx.");
            return;
        }

        if (!Path.GetExtension(_selectedFile.Name).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ToastService.Error("Помилка імпорту.", "Підтримується тільки формат .xlsx.");
            return;
        }

        try
        {
            _importing = true;
            _result = null;

            await using var stream = _selectedFile.OpenReadStream(15 * 1024 * 1024, _lifetimeCts.Token);
            _result = await ImportService.ImportExcelAsync(
                stream,
                _model.SelectedFileName ?? _selectedFile.Name,
                null,
                null,
                _lifetimeCts.Token);

            ToastService.Success("Імпорт завершено");

            if (Imported.HasDelegate)
                await Imported.InvokeAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка імпорту.", ex.Message);
        }
        finally
        {
            _importing = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CloseDrawerAsync()
    {
        if (IsOpenChanged.HasDelegate)
            await IsOpenChanged.InvokeAsync(false);
    }

    private async Task HandleDrawerClosedAsync()
    {
        ClearInput();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        GC.SuppressFinalize(this);
    }
}
