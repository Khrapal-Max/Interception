//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registry.Roles;

public partial class RoleCatalog : ComponentBase
{
    [Inject] private IParticipantRoleService RoleService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<ParticipantRole>? _roles;
    private bool _loading;
    private bool _drawerOpen;
    private ParticipantRole? _editingRole;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _roles = await RoleService.GetAllAsync();
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

    private void OpenCreate()
    {
        _editingRole = null;
        _drawerOpen = true;
    }

    private void OpenEdit(ParticipantRole role)
    {
        _editingRole = role;
        _drawerOpen = true;
    }
}
