//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.Drawers;

public partial class ResolvedParticipantDrawer : ComponentBase
{
    [Inject] private IResolvedParticipantService ResolvedService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public ResolvedParticipantDto? Participant { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private string? _name;
    private string? _role;
    private string? _division;
    private bool _nameError;
    private string? _serverError;
    private bool _saving;
    private bool _initialized;

    protected override void OnParametersSet()
    {
        if (!IsOpen) { _initialized = false; return; }
        if (_initialized) return;
        _initialized = true;

        _name = Participant?.Name ?? string.Empty;
        _role = Participant?.Role ?? string.Empty;
        _division = Participant?.Division ?? string.Empty;
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
        if (Participant is null) return;

        if (string.IsNullOrWhiteSpace(_name))
        {
            _nameError = true;
            return;
        }

        _saving = true;
        _serverError = null;
        try
        {
            await ResolvedService.UpdateAsync(
                Participant.Id,
                new ConfirmCandidateGroupDto
                {
                    Name = _name,
                    Role = _role,
                    Division = _division
                });

            Toasts.Success("Збережено", $"Особу «{_name}» оновлено.");
            await CloseAsync();
            await OnSaved.InvokeAsync();
        }
        catch (InvalidOperationException ex)
        {
            _serverError = ex.Message;
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
