//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registries.Persons;

/// <summary>
/// Реєстр підтверджених осіб.
/// </summary>
public partial class PersonsRegistry : ComponentBase
{
    [Inject] private IPersonRegistryService PersonRegistryService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<PersonRegistryItemDto>? _participants;
    private bool _loading;

    private bool _drawerOpen;
    private PersonRegistryItemDto? _editingParticipant;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _participants = await PersonRegistryService.GetAllAsync();
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

    private void OpenEdit(PersonRegistryItemDto participant)
    {
        _editingParticipant = participant;
        _drawerOpen = true;
    }
}
