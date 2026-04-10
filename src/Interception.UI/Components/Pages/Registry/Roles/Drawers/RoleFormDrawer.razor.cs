//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registry.Roles.Drawers;

public partial class RoleFormDrawer : ComponentBase
{
    [Inject] private IParticipantRoleService RoleService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public ParticipantRole? EditingRole { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private string? _name;
    private string? _description;
    private bool _nameError;
    private string? _serverError;
    private bool _saving;
    private bool _initialized;

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _initialized = false;
            return;
        }

        if (_initialized) return;
        _initialized = true;

        _name = EditingRole?.Name ?? string.Empty;
        _description = EditingRole?.Description ?? string.Empty;
        _nameError = false;
        _serverError = null;
    }

    private void OnDrawerClosed()
    {
        _initialized = false;
        _serverError = null;
        _nameError = false;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            _nameError = true;
            return;
        }

        _saving = true;
        _serverError = null;
        try
        {
            if (EditingRole is null)
            {
                await RoleService.CreateAsync(_name, _description ?? string.Empty);
                Toasts.Success("Додано", $"Роль «{_name}» додано до довідника.");
            }
            else
            {
                await RoleService.UpdateAsync(EditingRole.Id, _name, _description ?? string.Empty);
                Toasts.Success("Оновлено", $"Роль «{_name}» оновлено.");
            }

            await CloseAsync();
            await OnSaved.InvokeAsync();
        }
        catch (InvalidOperationException ex)
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

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
