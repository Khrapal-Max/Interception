//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Interception.UI.Components.Shared.Drawer;

public partial class Drawer : ComponentBase
{
    private ElementReference _panelRef;
    private bool _wasOpen;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter] public string Title { get; set; } = "Drawer";

    /// <summary>Контент всередині body (форми/контролі).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Опційні кнопки/дії в хедері (ліворуч від X).</summary>
    [Parameter] public RenderFragment? HeaderActions { get; set; }

    /// <summary>Опційний футер (Save/Cancel і т.п.).</summary>
    [Parameter] public RenderFragment? FooterContent { get; set; }

    /// <summary>Якщо true — не дозволяємо закривати по backdrop/esc/кнопці X.</summary>
    [Parameter] public bool DisableClose { get; set; }

    /// <summary>Закривати по кліку на backdrop.</summary>
    [Parameter] public bool CloseOnBackdrop { get; set; } = true;

    /// <summary>Закривати по ESC.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>Викликається перед закриттям. Поверни false щоб заблокувати закриття (наприклад, незбережені зміни).</summary>
    [Parameter] public Func<Task<bool>>? OnBeforeClose { get; set; }

    /// <summary>Подія після закриття.</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    private string BackdropAriaHidden => IsOpen ? "false" : "true";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // ✅ Фокус ставимо ТІЛЬКИ при відкритті (transition closed -> open)
        if (IsOpen && !_wasOpen)
        {
            _wasOpen = true;

            // фокус потрібен лише якщо хочеш ловити ESC навіть без кліку в контент
            await _panelRef.FocusAsync();
            return;
        }

        // ✅ при закритті — скидаємо флаг
        if (!IsOpen && _wasOpen)
            _wasOpen = false;
    }

    private async Task HandleBackdropClick()
    {
        if (!IsOpen || DisableClose || !CloseOnBackdrop)
            return;

        await CloseAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!IsOpen || DisableClose || !CloseOnEscape)
            return;

        // ✅ важливо: не роби нічого для інших клавіш
        if (e.Key is "Escape")
            await CloseAsync();
    }

    public async Task OpenAsync()
        => await SetOpenAsync(true);

    public async Task CloseAsync()
    {
        if (!IsOpen)
            return;

        if (DisableClose)
            return;

        if (OnBeforeClose is not null)
        {
            var canClose = await OnBeforeClose();
            if (!canClose)
                return;
        }

        await SetOpenAsync(false);
        await OnClosed.InvokeAsync();
    }

    private async Task SetOpenAsync(bool value)
    {
        if (IsOpen == value)
            return;

        IsOpen = value;
        await IsOpenChanged.InvokeAsync(value);
    }
}
