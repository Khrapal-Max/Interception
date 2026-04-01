//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Registry.Abstractions;
using Interception.Application.Toasts;
using Interception.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Registry.InterceptionActions.Drawers;

public partial class ActionFormDrawer : ComponentBase
{
    [Inject] private IInterceptionActionService ActionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Parameters
    // -------------------------------------------------------------------------

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>null = створення, не null = редагування</summary>
    [Parameter] public InterceptionAction? EditingAction { get; set; }

    [Parameter] public EventCallback OnSaved { get; set; }

    // -------------------------------------------------------------------------
    // Стан форми
    // -------------------------------------------------------------------------

    private string? _name;
    private string? _description;
    private bool _nameError;
    private string? _serverError;
    private bool _saving;
    private bool _initialized;

    // -------------------------------------------------------------------------
    // Lifecycle — заповнюємо форму при відкритті
    // -------------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _initialized = false;
            return;
        }

        if (_initialized) return;
        _initialized = true;

        // Заповнюємо поля з існуючої дії або очищаємо для нової
        _name = EditingAction?.Name ?? string.Empty;
        _description = EditingAction?.Description ?? string.Empty;
        _nameError = false;
        _serverError = null;
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _serverError = null;
        _nameError = false;
    }

    // -------------------------------------------------------------------------
    // Save / Close
    // -------------------------------------------------------------------------

    private async Task SaveAsync()
    {
        // Клієнтська валідація
        if (string.IsNullOrWhiteSpace(_name))
        {
            _nameError = true;
            return;
        }

        _saving = true;
        _serverError = null;
        try
        {
            if (EditingAction is null)
            {
                await ActionService.CreateAsync(_name, _description ?? string.Empty);
                Toasts.Success("Додано", $"Дію «{_name}» додано до довідника.");
            }
            else
            {
                await ActionService.UpdateAsync(
                    EditingAction.Id, _name, _description ?? string.Empty);
                Toasts.Success("Оновлено", $"Дію «{_name}» оновлено.");
            }

            await CloseAsync();
            await OnSaved.InvokeAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Конфлікт назви або не знайдено — показуємо в формі, не тост
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

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
