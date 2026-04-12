//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.ObservationJournal;

public partial class ObservationJournalPage : ComponentBase
{
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionQueryService InterceptionQueryService { get; set; } = default!;
    [Inject] private IInterceptionActionService InterceptionActionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Стан таблиці
    // -------------------------------------------------------------------------

    private PagedResultDto<InterceptionListItemDto>? _pagedResult;
    private bool _loading;
    private int _currentPage = 1;
    private const int PageSize = 50;

    private InterceptionFilterDto _filter = new();

    private bool HasActiveFilter =>
        _filter.DateFrom.HasValue ||
        _filter.DateTo.HasValue ||
        !string.IsNullOrWhiteSpace(_filter.Frequency) ||
        !string.IsNullOrWhiteSpace(_filter.VectorSignal) ||
        !string.IsNullOrWhiteSpace(_filter.ParticipantName) ||
        !string.IsNullOrWhiteSpace(_filter.LabelName);

    // -------------------------------------------------------------------------
    // Стан драверів — IsOpen binding
    // -------------------------------------------------------------------------

    private bool _formOpen;
    private Guid? _editingId;

    private bool _filterOpen;
    private bool _importOpen;
    private bool _textBlockOpen;
    private bool _deleteConfirmOpen;
    private Guid? _pendingDeleteId;
    private DateTime? _pendingDeleteObservedDate;
    private bool _deleteInProgress;

    private IReadOnlyList<InterceptionActionListItemDto> _actions = [];

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        _actions = await InterceptionActionService.GetAllAsync();
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // Дані
    // -------------------------------------------------------------------------

    internal async Task LoadPageAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            _pagedResult = await InterceptionQueryService.GetPagedAsync(
                _filter, _currentPage, PageSize);
        }
        catch (Exception ex)
        {
            var error = MapDomainError(ex, "load");
            Toasts.Error(error.Title, error.Hint);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task GoToPageAsync(int pageNum)
    {
        if (_pagedResult is null)
            return;

        _currentPage = Math.Clamp(pageNum, 1, Math.Max(1, _pagedResult.TotalPages));
        await LoadPageAsync();
    }

    private IEnumerable<int?> GetVisiblePages()
    {
        if (_pagedResult is null || _pagedResult.TotalPages <= 1)
            yield break;

        var total = _pagedResult.TotalPages;
        var current = _currentPage;

        if (total <= 7)
        {
            for (var i = 1; i <= total; i++)
                yield return i;

            yield break;
        }

        yield return 1;

        var start = Math.Max(2, current - 1);
        var end = Math.Min(total - 1, current + 1);

        if (start > 2)
            yield return null;

        for (var i = start; i <= end; i++)
            yield return i;

        if (end < total - 1)
            yield return null;

        yield return total;
    }

    // -------------------------------------------------------------------------
    // Відкриття драверів
    // -------------------------------------------------------------------------

    private void OpenCreate()
    {
        _editingId = null;
        _formOpen = true;
    }

    private void OpenEdit(Guid id)
    {
        _editingId = id;
        _formOpen = true;
    }

    private void OpenFilter() => _filterOpen = true;
    private void OpenImport() => _importOpen = true;
    private void OpenTextBlock() => _textBlockOpen = true;

    // -------------------------------------------------------------------------
    // Callbacks від драверів
    // -------------------------------------------------------------------------

    private async Task OnFilterApplied(InterceptionFilterDto filter)
    {
        _filter = filter;
        _currentPage = 1;
        await LoadPageAsync();
    }

    private async Task OnFilterReset()
    {
        _filter = new InterceptionFilterDto();
        _currentPage = 1;
        await LoadPageAsync();
    }

    private async Task ResetFilter()
    {
        _filter = new InterceptionFilterDto();
        _currentPage = 1;
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // Видалення
    // -------------------------------------------------------------------------

    private void AskDeleteConfirmation(Guid id, DateTime observedDate)
    {
        _pendingDeleteId = id;
        _pendingDeleteObservedDate = observedDate;
        _deleteConfirmOpen = true;
    }

    private void CancelDeleteConfirmation()
    {
        if (_deleteInProgress)
            return;

        _deleteConfirmOpen = false;
        _pendingDeleteId = null;
        _pendingDeleteObservedDate = null;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDeleteId is null || _pendingDeleteObservedDate is null)
            return;

        _deleteInProgress = true;

        try
        {
            await InterceptionCommandService.DeleteAsync(_pendingDeleteId.Value);
            Toasts.Success("Видалено", $"Запис від {ConverterDateTimeExtensions.ToDisplay(_pendingDeleteObservedDate.Value):dd.MM HH:mm} видалено.");
            await LoadPageAsync();
            _deleteConfirmOpen = false;
            _pendingDeleteId = null;
            _pendingDeleteObservedDate = null;
        }
        catch (Exception ex)
        {
            var error = MapDomainError(ex, "delete");
            Toasts.Error(error.Title, error.Hint);
        }
        finally
        {
            _deleteInProgress = false;
        }
    }

    private static (string Title, string Hint) MapDomainError(Exception ex, string operation)
    {
        if (Contains(ex.Message, "не знайдено"))
        {
            return operation == "delete"
                ? ("Запис вже недоступний", "Схоже, observation вже видалено або змінено в іншій сесії. Оновіть список і спробуйте ще раз.")
                : ("Дані оновились", "Частину записів вже змінено. Оновіть сторінку для актуального стану.");
        }

        if (IsExceptionType(ex, "EntityNotFoundDomainException"))
        {
            return operation == "delete"
                ? ("Запис вже недоступний", "Observation не знайдено. Оновіть список та перевірте фільтри.")
                : ("Дані не знайдено", "Частину довідкових даних не знайдено. Оновіть сторінку.");
        }

        if (IsExceptionType(ex, "AggregateStateViolationException"))
            return ("Дія недоступна", "Запис зараз у стані, який не дозволяє цю операцію. Перевірте пов'язані зміни та повторіть пізніше.");

        if (IsExceptionType(ex, "DomainException"))
            return ("Порушено бізнес-правило", "Операцію зупинено правилами домену. Перевірте пов'язані поля або стан запису.");

        return ex switch
        {
            ArgumentException => ("Некоректні вхідні дані", "Перевірте фільтри/параметри, оновіть сторінку та повторіть дію."),

            InvalidOperationException => ("Операцію не виконано", "Стан даних змінився. Оновіть список і повторіть дію."),

            _ => operation == "delete"
                ? ("Помилка видалення", "Не вдалося видалити observation. Спробуйте ще раз або зверніться до адміністратора.")
                : ("Помилка завантаження", "Не вдалося завантажити дані. Перевірте з'єднання та повторіть спробу.")
        };
    }

    private static bool IsExceptionType(Exception ex, string typeName)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (string.Equals(current.GetType().Name, typeName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool Contains(string? value, string pattern) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
}
