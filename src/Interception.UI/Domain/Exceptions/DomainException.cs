//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Exceptions;

/// <summary>
/// Базовий typed exception для доменних порушень.
/// </summary>
public abstract class DomainException(string message) : InvalidOperationException(message)
{
}
