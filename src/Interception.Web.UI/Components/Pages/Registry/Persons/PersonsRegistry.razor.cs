//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Registry.Abstractions;
using Interception.Application.Registry.Dtos;
using Interception.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Pages.Registry.Persons;

/// <summary>
/// Єдиний реєстр усіх не-НВ осіб у системі.
/// </summary>
public partial class PersonsRegistry : ComponentBase
{
    [Inject] private IPersonRegistryService PersonRegistryService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<PersonRegistryItemDto>? _participants;
    private bool _loading;

    private bool _drawerOpen;
    private PersonRegistryItemDto? _editingParticipant;

    private int ConfirmedCount => _participants?.Count(x => x.IsConfirmed) ?? 0;
    private int ObservedCount => _participants?.Count(x => !x.IsConfirmed) ?? 0;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    /// <summary>
    /// Завантажує осіб реєстру.
    /// </summary>
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
            StateHasChanged();
        }
    }

    /// <summary>
    /// Відкриває дравер редагування вибраної особи.
    /// </summary>
    /// <param name="participant">Рядок реєстру.</param>
    private void OpenEdit(PersonRegistryItemDto participant)
    {
        _editingParticipant = participant;
        _drawerOpen = true;
    }
}
