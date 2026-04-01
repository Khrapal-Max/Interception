//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
//-----------------------------------------------------------------------------
// ToastService
//-----------------------------------------------------------------------------

namespace Interception.Application.Toasts;

public sealed class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(ToastMessage msg) =>
        OnShow?.Invoke(msg);

    public void Info(string title, string? body = null) =>
        Show(new(ToastKind.Info, title, body));

    public void Success(string title, string? body = null) =>
        Show(new(ToastKind.Success, title, body));

    public void Warning(string title, string? body = null) =>
        Show(new(ToastKind.Warning, title, body));

    public void Error(string title, string? body = null) =>
        Show(new(ToastKind.Error, title, body, AutoHideMs: 7000));
}