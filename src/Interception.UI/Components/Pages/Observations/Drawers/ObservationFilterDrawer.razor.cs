//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Components.Shared.Drawer;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

/// <summary>
/// Drawer для редагування фільтра реєстру спостережень.
/// </summary>
public partial class ObservationFilterDrawer : ComponentBase
{
    private Drawer? _drawer;
    private ObservationFilterModel _model = new();
    private bool _initialized;

    /// <summary>
    /// Ознака відкритого стану drawer.
    /// </summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>
    /// Callback зміни стану drawer.
    /// </summary>
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>
    /// Поточний фільтр сторінки.
    /// </summary>
    [Parameter] public ObservationFilterModel? Model { get; set; }

    /// <summary>
    /// Callback застосування фільтра.
    /// </summary>
    [Parameter] public EventCallback<ObservationFilterModel> Applied { get; set; }

    /// <summary>
    /// Підготовка локальної копії моделі при відкритті drawer.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _initialized = false;
            return;
        }

        if (_initialized)
            return;

        _initialized = true;
        _model = Clone(Model);
    }

    /// <summary>
    /// Застосовує вибраний фільтр і закриває drawer.
    /// </summary>
    private async Task ApplyAsync()
    {
        await Applied.InvokeAsync(Clone(_model));
        await CloseAsync();
    }

    /// <summary>
    /// Очищає всі поля фільтра у drawer.
    /// </summary>
    private Task ResetAsync()
    {
        _model = new ObservationFilterModel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Закриває drawer після завершення взаємодії.
    /// </summary>
    private Task HandleClosedAsync()
    {
        _initialized = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Оновлює нижню межу часу спостереження.
    /// </summary>
    private void OnObservedFromChanged(ChangeEventArgs args)
        => _model.ObservedFrom = ParseDateTimeLocal(args.Value?.ToString());

    /// <summary>
    /// Оновлює верхню межу часу спостереження.
    /// </summary>
    private void OnObservedToChanged(ChangeEventArgs args)
        => _model.ObservedTo = ParseDateTimeLocal(args.Value?.ToString());

    /// <summary>
    /// Закриває drawer через shared-компонент.
    /// </summary>
    private async Task CloseAsync()
    {
        if (_drawer is not null)
        {
            await _drawer.CloseAsync();
            return;
        }

        await IsOpenChanged.InvokeAsync(false);
    }

    /// <summary>
    /// Форматує дату для HTML datetime-local.
    /// </summary>
    private static string FormatDateTimeLocal(DateTime? value)
        => value?.ToString("yyyy-MM-ddTHH:mm") ?? string.Empty;

    /// <summary>
    /// Парсить значення HTML datetime-local у <see cref="DateTime"/>.
    /// </summary>
    private static DateTime? ParseDateTimeLocal(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    /// <summary>
    /// Створює локальну копію фільтра для безпечного редагування у drawer.
    /// </summary>
    private static ObservationFilterModel Clone(ObservationFilterModel? model)
        => model is null
            ? new ObservationFilterModel()
            : new ObservationFilterModel
            {
                Query = model.Query,
                ObservedFrom = model.ObservedFrom,
                ObservedTo = model.ObservedTo,
                SubdivisionStrength = model.SubdivisionStrength,
                OnlyUnknownParticipants = model.OnlyUnknownParticipants,
                OnlyBoundAction = model.OnlyBoundAction
            };
}
