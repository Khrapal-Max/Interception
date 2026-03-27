/*//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registries.ResolvedParticipants;

/// <summary>
/// Реєстр підтверджених осіб.
/// </summary>
public partial class ResolvedParticipantRegistry : ComponentBase
{
    [Inject] private IResolvedParticipantService ResolvedService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private IReadOnlyList<ResolvedParticipantDto>? _participants;
    private bool _loading;

    private bool _drawerOpen;
    private ResolvedParticipantDto? _editingParticipant;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    /// <summary>
    /// Завантажує список підтверджених осіб.
    /// </summary>
    internal async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _participants = await ResolvedService.GetAllAsync();
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

    /// <summary>
    /// Відкриває drawer редагування підтвердженої особи.
    /// </summary>
    /// <param name="participant">Особа для редагування.</param>
    private void OpenEdit(ResolvedParticipantDto participant)
    {
        _editingParticipant = participant;
        _drawerOpen = true;
    }
}
*/