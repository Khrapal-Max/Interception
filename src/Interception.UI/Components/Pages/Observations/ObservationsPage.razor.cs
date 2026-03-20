//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations;

public partial class ObservationsPage : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();

    private ObservationRegistryFilterDto _filter = new()
    {
        Skip = 0,
        Take = 25
    };

    private ObservationRegistryPageDto _page = new([], 0);
    private bool _loading;
    private bool _createDrawerOpen;
    private bool _importDrawerOpen;
    private DateTime? _observedFrom;
    private DateTime? _observedTo;
    private string? _subdivisionStrengthText;

    private bool _radioDrawerOpen;
    private string? _observedFromText;
    private string? _observedToText;

    [Inject] private IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        SyncUiFromFilter();
        await LoadAsync();
    }

    private async Task SearchAsync()
    {
        _filter.Skip = 0;
        ApplyUiFilterValues();
        await LoadAsync();
    }

    private async Task RefreshAsync()
    {
        ApplyUiFilterValues();
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _filter = new ObservationRegistryFilterDto
        {
            Skip = 0,
            Take = 25
        };

        SyncUiFromFilter();
        await LoadAsync();
    }

    private async Task PreviousAsync()
    {
        if (_filter.Skip <= 0)
            return;

        _filter.Skip = Math.Max(0, _filter.Skip - _filter.Take);
        await LoadAsync();
    }

    private async Task NextAsync()
    {
        var next = _filter.Skip + _filter.Take;
        if (next >= _page.TotalCount)
            return;

        _filter.Skip = next;
        await LoadAsync();
    }

    private void OpenCreateDrawer() => _createDrawerOpen = true;

    private void OpenImportDrawer() => _importDrawerOpen = true;

    private void OpenRadioDrawer() => _radioDrawerOpen = true;

    private void OpenDetails(Guid observationId) => Navigation.NavigateTo($"/observations/{observationId}");

    private Task HandleCreateDrawerChangedAsync(bool isOpen)
    {
        _createDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    private Task HandleRadioDrawerChangedAsync(bool isOpen)
    {
        _radioDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    private Task HandleImportDrawerChangedAsync(bool isOpen)
    {
        _importDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    private async Task HandleSavedAsync(Guid observationId)
    {
        _createDrawerOpen = false;
        await LoadAsync();
        Navigation.NavigateTo($"/observations/{observationId}");
    }

    private async Task HandleRadioSavedAsync(Guid observationId)
    {
        _radioDrawerOpen = false;
        await LoadAsync();
        Navigation.NavigateTo($"/observations/{observationId}");
    }

    private async Task HandleImportedAsync()
    {
        _importDrawerOpen = false;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _page = await RegistryService.SearchAsync(_filter, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження реєстру.", ex.Message);
            _page = new ObservationRegistryPageDto([], 0);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyUiFilterValues()
    {
        _filter.ObservedFrom = _observedFrom;
        _filter.ObservedTo = _observedTo;
        _filter.SubdivisionStrength = ParseStrength(_subdivisionStrengthText);
    }

    private void SyncUiFromFilter()
    {
        _observedFrom = _filter.ObservedFrom;
        _observedTo = _filter.ObservedTo;
        _subdivisionStrengthText = _filter.SubdivisionStrength switch
        {
            SubdivisionLinkStrength.Weak => "1",
            SubdivisionLinkStrength.Medium => "2",
            SubdivisionLinkStrength.Strong => "3",
            _ => null
        };
    }

    private void OnObservedFromChanged(ChangeEventArgs args)
        => _observedFrom = ParseDateTimeLocal(args.Value?.ToString());

    private void OnObservedToChanged(ChangeEventArgs args)
        => _observedTo = ParseDateTimeLocal(args.Value?.ToString());

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    private static string FormatDateTimeLocal(DateTime? value)
        => value?.ToString("yyyy-MM-ddTHH:mm") ?? string.Empty;

    private static DateTime? ParseDateTimeLocal(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static SubdivisionLinkStrength? ParseStrength(string? value)
        => value switch
        {
            "1" => SubdivisionLinkStrength.Weak,
            "2" => SubdivisionLinkStrength.Medium,
            "3" => SubdivisionLinkStrength.Strong,
            _ => null
        };
}
