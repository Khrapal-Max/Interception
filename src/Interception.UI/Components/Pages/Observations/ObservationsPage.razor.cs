//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations;

/// <summary>
/// Сторінка реєстру спостережень.
/// </summary>
public partial class ObservationsPage : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();

    private ObservationRegistryFilterDto _filter = new()
    {
        Skip = 0,
        Take = 25
    };

    private ObservationRegistryPageDto _page = new([], 0);
    private ObservationFilterModel _filterModel = new();
    private bool _loading;
    private bool _filterDrawerOpen;
    private bool _createDrawerOpen;
    private bool _importDrawerOpen;
    private bool _radioDrawerOpen;
    private ObservationCreateSeedModel? _pendingCreateSeed;

    [Inject] private IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    /// <summary>
    /// Ознака наявності активних фільтрів на сторінці.
    /// </summary>
    private bool HasActiveFilters => BuildFilterChips().Count > 0;

    #region Lifecycle

    /// <summary>
    /// Ініціалізує сторінку та завантажує реєстр.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        SyncFilterModelFromDto();
        await LoadAsync();
    }

    #endregion

    #region Toolbar and navigation

    /// <summary>
    /// Відкриває drawer фільтра.
    /// </summary>
    private void OpenFilterDrawer()
    {
        SyncFilterModelFromDto();
        _filterDrawerOpen = true;
    }

    /// <summary>
    /// Відкриває drawer створення спостереження.
    /// </summary>
    private void OpenCreateDrawer() => _createDrawerOpen = true;

    /// <summary>
    /// Відкриває drawer імпорту спостережень.
    /// </summary>
    private void OpenImportDrawer() => _importDrawerOpen = true;

    /// <summary>
    /// Відкриває drawer парсингу службового радіообміну.
    /// </summary>
    private void OpenRadioDrawer() => _radioDrawerOpen = true;

    /// <summary>
    /// Переходить на сторінку деталізації вибраного спостереження.
    /// </summary>
    private void OpenDetails(Guid observationId) => Navigation.NavigateTo($"/observations/{observationId}");

    #endregion

    #region Drawer callbacks

    /// <summary>
    /// Оновлює стан drawer фільтра.
    /// </summary>
    private Task HandleFilterDrawerChangedAsync(bool isOpen)
    {
        _filterDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Застосовує новий фільтр із drawer та перезавантажує сторінку.
    /// </summary>
    private async Task HandleFilterAppliedAsync(ObservationFilterModel model)
    {
        _filterModel = CloneFilterModel(model);
        _filter.Skip = 0;
        ApplyFilterModelToDto();
        await LoadAsync();
    }

    /// <summary>
    /// Оновлює стан drawer створення.
    /// </summary>
    private Task HandleCreateDrawerChangedAsync(bool isOpen)
    {
        _createDrawerOpen = isOpen;

        if (!isOpen)
            _pendingCreateSeed = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Оновлює стан drawer парсингу службового р/о.
    /// </summary>
    private Task HandleRadioDrawerChangedAsync(bool isOpen)
    {
        _radioDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Приймає seed після парсингу службового р/о та відкриває create drawer.
    /// </summary>
    private Task HandleRadioParsedAsync(ObservationCreateSeedModel seed)
    {
        _pendingCreateSeed = seed;
        _radioDrawerOpen = false;
        _createDrawerOpen = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Оновлює стан drawer імпорту.
    /// </summary>
    private Task HandleImportDrawerChangedAsync(bool isOpen)
    {
        _importDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обробляє успішне збереження спостереження.
    /// </summary>
    private async Task HandleSavedAsync(Guid observationId)
    {
        _pendingCreateSeed = null;
        _createDrawerOpen = false;
        await LoadAsync();
        Navigation.NavigateTo($"/observations/{observationId}");
    }

    /// <summary>
    /// Обробляє успішне завершення імпорту.
    /// </summary>
    private async Task HandleImportedAsync()
    {
        _importDrawerOpen = false;
        await LoadAsync();
    }

    #endregion

    #region Page actions

    /// <summary>
    /// Перезавантажує сторінку з поточним фільтром.
    /// </summary>
    private async Task RefreshAsync()
    {
        ApplyFilterModelToDto();
        await LoadAsync();
    }

    /// <summary>
    /// Скидає фільтр сторінки до початкового стану.
    /// </summary>
    private async Task ClearFiltersAsync()
    {
        _filter = new ObservationRegistryFilterDto
        {
            Skip = 0,
            Take = 25
        };

        SyncFilterModelFromDto();
        await LoadAsync();
    }

    /// <summary>
    /// Переходить на попередню сторінку пагінації.
    /// </summary>
    private async Task PreviousAsync()
    {
        if (_filter.Skip <= 0)
            return;

        _filter.Skip = Math.Max(0, _filter.Skip - _filter.Take);
        await LoadAsync();
    }

    /// <summary>
    /// Переходить на наступну сторінку пагінації.
    /// </summary>
    private async Task NextAsync()
    {
        var next = _filter.Skip + _filter.Take;
        if (next >= _page.TotalCount)
            return;

        _filter.Skip = next;
        await LoadAsync();
    }

    #endregion

    #region Loading

    /// <summary>
    /// Завантажує реєстр спостережень за поточним фільтром.
    /// </summary>
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

    #endregion

    #region Filter mapping and presentation

    /// <summary>
    /// Переносить значення з UI-моделі фільтра в DTO запиту.
    /// </summary>
    private void ApplyFilterModelToDto()
    {
        _filter.Query = NormalizeText(_filterModel.Query);
        _filter.ObservedFrom = _filterModel.ObservedFrom;
        _filter.ObservedTo = _filterModel.ObservedTo;
        _filter.SubdivisionStrength = _filterModel.SubdivisionStrength;
        _filter.OnlyUnknownParticipants = _filterModel.OnlyUnknownParticipants;
        _filter.OnlyBoundAction = _filterModel.OnlyBoundAction;
    }

    /// <summary>
    /// Синхронізує UI-модель фільтра з DTO сторінки.
    /// </summary>
    private void SyncFilterModelFromDto()
    {
        _filterModel = new ObservationFilterModel
        {
            Query = _filter.Query,
            ObservedFrom = _filter.ObservedFrom,
            ObservedTo = _filter.ObservedTo,
            SubdivisionStrength = _filter.SubdivisionStrength,
            OnlyUnknownParticipants = _filter.OnlyUnknownParticipants,
            OnlyBoundAction = _filter.OnlyBoundAction
        };
    }

    /// <summary>
    /// Формує короткий текстовий стан поточного фільтра.
    /// </summary>
    private string GetFilterSummaryText()
        => HasActiveFilters ? "Фільтр застосовано" : "Фільтр не застосовано";

    /// <summary>
    /// Формує набір візуальних міток активного фільтра.
    /// </summary>
    private List<string> BuildFilterChips()
    {
        var chips = new List<string>();

        if (!string.IsNullOrWhiteSpace(_filter.Query))
            chips.Add($"Пошук: {_filter.Query}");

        if (_filter.ObservedFrom is not null)
            chips.Add($"Від: {_filter.ObservedFrom.Value:dd.MM.yyyy HH:mm}");

        if (_filter.ObservedTo is not null)
            chips.Add($"До: {_filter.ObservedTo.Value:dd.MM.yyyy HH:mm}");

        if (_filter.SubdivisionStrength is not null)
            chips.Add($"Зв'язок: {GetStrengthText(_filter.SubdivisionStrength.Value)}");

        if (_filter.OnlyUnknownParticipants)
            chips.Add("Лише НВ");

        if (_filter.OnlyBoundAction)
            chips.Add("Лише типізовані");

        return chips;
    }

    #endregion

    #region Helpers and disposal

    /// <summary>
    /// Створює безпечну копію UI-моделі фільтра.
    /// </summary>
    private static ObservationFilterModel CloneFilterModel(ObservationFilterModel model)
        => new()
        {
            Query = model.Query,
            ObservedFrom = model.ObservedFrom,
            ObservedTo = model.ObservedTo,
            SubdivisionStrength = model.SubdivisionStrength,
            OnlyUnknownParticipants = model.OnlyUnknownParticipants,
            OnlyBoundAction = model.OnlyBoundAction
        };

    /// <summary>
    /// Нормалізує текстове значення фільтра.
    /// </summary>
    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Повертає локалізований текст сили зв'язку підрозділу.
    /// </summary>
    private static string GetStrengthText(SubdivisionLinkStrength value)
        => value switch
        {
            SubdivisionLinkStrength.Weak => "Слабкий",
            SubdivisionLinkStrength.Medium => "Середній",
            SubdivisionLinkStrength.Strong => "Сильний",
            _ => value.ToString()
        };

    /// <summary>
    /// Вивільняє ресурси сторінки.
    /// </summary>
    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
