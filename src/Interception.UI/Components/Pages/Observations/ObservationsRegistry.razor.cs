//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Observations.Enums;
using Interception.UI.Application.Observations.Models;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations;

public partial class ObservationsRegistry : ComponentBase
{
    [Inject] public IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    private ObservationRegistryPageDto? _page;
    private bool _loading;

    private int _pageIndex = 0;

    private Guid? _detailsId;

    private DrawerKind _activeDrawer = DrawerKind.None;

    // Single draft instance bound to filter drawer
    private FilterDraftModel _filterDraft = new();

    private int Take => Math.Clamp(_filterDraft.Take, 1, 500);

    private int TotalPages => _page is null ? 1 : Math.Max(1, (int)Math.Ceiling(_page.Total / (double)Take));
    private bool CanPrev => _pageIndex > 0;
    private bool CanNext => _pageIndex + 1 < TotalPages;

    private bool IsFilterOpen => _activeDrawer == DrawerKind.Filter;
    private bool IsImportOpen => _activeDrawer == DrawerKind.Import;

    protected override async Task OnInitializedAsync()
    {
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            var filter = BuildFilter();
            _page = await RegistryService.SearchAsync(filter, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error(ex.Message);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private ObservationRegistryFilter BuildFilter()
        => new()
        {
            DateFrom = _filterDraft.DateFrom is null ? null : DateOnly.FromDateTime(_filterDraft.DateFrom.Value),
            DateTo = _filterDraft.DateTo is null ? null : DateOnly.FromDateTime(_filterDraft.DateTo.Value),
            DayPart = _filterDraft.DayPart,
            Person = string.IsNullOrWhiteSpace(_filterDraft.Person) ? null : _filterDraft.Person,
            Action = string.IsNullOrWhiteSpace(_filterDraft.Action) ? null : _filterDraft.Action,
            Location = string.IsNullOrWhiteSpace(_filterDraft.Location) ? null : _filterDraft.Location,
            District = string.IsNullOrWhiteSpace(_filterDraft.District) ? null : _filterDraft.District,
            Rm = string.IsNullOrWhiteSpace(_filterDraft.Rm) ? null : _filterDraft.Rm,
            Skip = _pageIndex * Take,
            Take = Take
        };

    private async Task PrevPageAsync()
    {
        if (!CanPrev) return;
        _pageIndex--;
        await SearchAsync();
    }

    private async Task NextPageAsync()
    {
        if (!CanNext) return;
        _pageIndex++;
        await SearchAsync();
    }

    private async Task ResetAsync()
    {
        _filterDraft = new FilterDraftModel();
        _pageIndex = 0;
        await SearchAsync();
    }

    private async Task OpenDetailsAsync(Guid id)
    {
        _detailsId = id;
        await Task.CompletedTask;
    }

    private void CloseDetails() => _detailsId = null;

    private void OpenFilter() => _activeDrawer = DrawerKind.Filter;
    private void OpenImport() => _activeDrawer = DrawerKind.Import;

    private void OpenCreatePage() => Navigation.NavigateTo("/observations/create");

    private void CloseDrawer() => _activeDrawer = DrawerKind.None;

    private async Task ApplyFilterAsync()
    {
        _pageIndex = 0;
        await SearchAsync();
    }

    private async Task ClearFilterAsync()
    {
        _filterDraft = new FilterDraftModel();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnImportedAsync()
    {
        _pageIndex = 0;
        await SearchAsync();
    }

    private async Task OnCreatedAsync()
    {
        _pageIndex = 0;
        await SearchAsync();
    }
}
