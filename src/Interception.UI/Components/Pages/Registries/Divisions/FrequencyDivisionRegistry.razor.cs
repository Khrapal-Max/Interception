//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registries.Divisions;

/// <summary>
/// Реєстр частот з можливістю коригування закріпленого підрозділу.
/// </summary>
public partial class FrequencyDivisionRegistry : ComponentBase
{
    [Inject] private IFrequencyDivisionService FrequencyDivisionService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<FrequencySuggestionDto>? _items;
    private bool _loading;

    private bool _drawerOpen;
    private FrequencySuggestionDto? _editingItem;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    /// <summary>
    /// Завантажує список частот і закріплених підрозділів.
    /// </summary>
    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _items = await FrequencyDivisionService.GetAllAsync();
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
    /// Відкриває дравер коригування для вибраної частоти.
    /// </summary>
    /// <param name="item">Рядок реєстру.</param>
    private void OpenEdit(FrequencySuggestionDto item)
    {
        _editingItem = item;
        _drawerOpen = true;
    }
}
