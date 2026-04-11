//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Exceptions;

public sealed class AggregateStateViolationException(string message)
    : DomainException(message);
