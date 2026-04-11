//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Exceptions;

public sealed class DuplicateParticipantException(string participantName)
    : DomainException($"Учасник '{participantName}' вже існує в цьому перехопленні.");
