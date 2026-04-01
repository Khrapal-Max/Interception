//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Dtos;
using Interception.Application.Registry.Abstractions;
using Interception.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Registry.Divisions.Drawers;

/// <summary>
/// Дравер коригування підрозділу для вибраної частоти.
/// </summary>
public partial class FrequencyDivisionDrawer : ComponentBase
{
    [Inject] private IFrequencyDivisionService FrequencyDivisionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public FrequencySuggestionDto? EditingItem { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private string? _frequency;
    private string? _division;
    private bool _divisionError;
    private string? _serverError;
    private bool _saving;
    private bool _initialized;

    /// <inheritdoc />
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
        _frequency = EditingItem?.Frequency ?? string.Empty;
        _division = EditingItem?.Division ?? string.Empty;
        _divisionError = false;
        _serverError = null;
    }

    /// <summary>
    /// Скидає локальний стан після закриття дравера.
    /// </summary>
    private void OnDrawerClosed()
    {
        _initialized = false;
        _divisionError = false;
        _serverError = null;
    }

    /// <summary>
    /// Зберігає коригування підрозділу для частоти.
    /// </summary>
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_division))
        {
            _divisionError = true;
            return;
        }

        _saving = true;
        _serverError = null;
        try
        {
            await FrequencyDivisionService.CorrectDivisionAsync(
                _frequency ?? string.Empty,
                _division,
                CancellationToken.None);

            Toasts.Success("Оновлено", $"Підрозділ для частоти «{_frequency}» оновлено.");
            await CloseAsync();
            await OnSaved.InvokeAsync();
        }
        catch (ArgumentException ex)
        {
            _serverError = ex.Message;
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка збереження", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// Закриває дравер.
    /// </summary>
    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
