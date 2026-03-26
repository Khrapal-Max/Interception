//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Catalogs;

public partial class ActionCatalog : ComponentBase
{
    [Inject] private IInterceptionActionService ActionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<InterceptionAction>? _actions;
    private bool _loading;

    // Дравер
    private bool _drawerOpen;
    private InterceptionAction? _editingAction; // null = створення

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    // -------------------------------------------------------------------------
    // Дані
    // -------------------------------------------------------------------------

    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _actions = await ActionService.GetAllAsync();
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

    // -------------------------------------------------------------------------
    // Відкриття дравера
    // -------------------------------------------------------------------------

    private void OpenCreate()
    {
        _editingAction = null;
        _drawerOpen = true;
    }

    private void OpenEdit(InterceptionAction action)
    {
        _editingAction = action;
        _drawerOpen = true;
    }
}
