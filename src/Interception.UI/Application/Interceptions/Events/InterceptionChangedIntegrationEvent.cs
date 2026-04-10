//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Common.Events;

namespace Interception.UI.Application.Interceptions.Events;

public enum InterceptionChangeType
{
    Created = 1,
    Updated = 2,
    Deleted = 3
}

/// <summary>
/// Подія про зміну запису перехоплення для downstream контекстів.
/// </summary>
public sealed record InterceptionChangedIntegrationEvent(
    Guid InterceptionMessageId,
    InterceptionChangeType ChangeType,
    DateTime OccurredAtUtc) : IIntegrationEvent;
