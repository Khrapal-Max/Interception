//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
//-----------------------------------------------------------------------------
// ToastMessage
//-----------------------------------------------------------------------------

namespace Interception.Application.Toasts;

public enum ToastKind { Info, Success, Warning, Error }

public sealed record ToastMessage(
    ToastKind Kind,
    string Title,
    string? Body = null,
    int AutoHideMs = 4000
);