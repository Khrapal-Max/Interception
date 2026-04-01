//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
//-----------------------------------------------------------------------------
// ToastContainer
//-----------------------------------------------------------------------------

using Interception.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.Web.UI.Components.Shared.ToastContainer;

public partial class ToastContainer : ComponentBase, IDisposable
{
    private readonly List<ToastMessage> _toasts = [];

    [Inject] private ToastService Toasts { get; set; } = default!;

    protected override void OnInitialized()
    {
        Toasts.OnShow += Show;
    }

    private void Show(ToastMessage msg)
    {
        _toasts.Insert(0, msg);
        InvokeAsync(StateHasChanged);

        if (msg.AutoHideMs > 0)
        {
            _ = AutoHideAsync(msg);
        }
    }

    private async Task AutoHideAsync(ToastMessage msg)
    {
        await Task.Delay(msg.AutoHideMs);
        Remove(msg);
    }

    private void Remove(ToastMessage msg)
    {
        if (_toasts.Remove(msg))
            InvokeAsync(StateHasChanged);
    }

    private static string IconClass(ToastKind kind) => kind switch
    {
        ToastKind.Success => "bi bi-check-circle-fill text-success",
        ToastKind.Warning => "bi bi-exclamation-triangle-fill text-warning",
        ToastKind.Error => "bi bi-x-circle-fill text-danger",
        _ => "bi bi-info-circle-fill text-primary"
    };

    public void Dispose()
    {
        Toasts.OnShow -= Show;
        GC.SuppressFinalize(this);
    }
}
