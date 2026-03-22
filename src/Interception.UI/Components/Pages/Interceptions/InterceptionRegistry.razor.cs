//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions;

public partial class InterceptionRegistry : ComponentBase
{
    [Inject] private IInterceptionService InterceptionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

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

    private bool _formOpen;
    private Guid? _editingId;
    private bool _filterOpen;
    private bool _importOpen;

    protected override async Task OnInitializedAsync()
        => await LoadPageAsync();

    internal async Task LoadPageAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            _pagedResult = await InterceptionService.GetPagedAsync(
                _filter, _currentPage, PageSize);
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

    private async Task GoToPageAsync(int pageNum)
    {
        _currentPage = pageNum;
        await LoadPageAsync();
    }

    private void OpenCreate() { _editingId = null; _formOpen = true; }
    private void OpenEdit(Guid id) { _editingId = id; _formOpen = true; }
    private void OpenFilter() => _filterOpen = true;
    private void OpenImport() => _importOpen = true;

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

    private async Task ConfirmDeleteAsync(Guid id, DateTime observedDate)
    {
        try
        {
            await InterceptionService.DeleteAsync(id);
            Toasts.Success("Видалено", $"Запис від {observedDate:dd.MM HH:mm} видалено.");
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка видалення", ex.Message);
        }
    }
}
