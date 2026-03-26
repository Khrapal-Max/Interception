//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions;

public partial class InterceptionRegistry : ComponentBase
{
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private IInterceptionQueryService InterceptionQueryService { get; set; } = default!;
    [Inject] private IInterceptionActionService InterceptionActionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Стан таблиці
    // -------------------------------------------------------------------------

    private PagedResult<InterceptionListItemDto>? _pagedResult;
    private bool _loading;
    private int _currentPage = 1;
    private const int PageSize = 50;

    private InterceptionFilter _filter = new();

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

    private IReadOnlyList<InterceptionAction> _actions = [];

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
            Toasts.Error("Помилка завантаження", ex.Message);
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

    private async Task OnFilterApplied(InterceptionFilter filter)
    {
        _filter = filter;
        _currentPage = 1;
        await LoadPageAsync();
    }

    private async Task OnFilterReset()
    {
        _filter = new InterceptionFilter();
        _currentPage = 1;
        await LoadPageAsync();
    }

    private async Task ResetFilter()
    {
        _filter = new InterceptionFilter();
        _currentPage = 1;
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // Видалення
    // -------------------------------------------------------------------------

    private async Task DeleteAsync(Guid id, DateTime observedDate)
    {
        // TODO: замінити на модальний діалог підтвердження
        try
        {
            await InterceptionCommandService.DeleteAsync(id);
            Toasts.Success("Видалено", $"Запис від {observedDate:dd.MM HH:mm} видалено.");
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка видалення", ex.Message);
        }
    }
}
