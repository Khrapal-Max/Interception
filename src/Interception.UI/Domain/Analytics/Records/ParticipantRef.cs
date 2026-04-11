//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Analytics.Records;

/// <summary>
/// Незмінне посилання на конкретного учасника в конкретному повідомленні.
/// Використовується в ParticipantCandidateGroup щоб зберегти прив'язку
/// навіть після перейменування учасника.
/// </summary>
public sealed record ParticipantRef(
    Guid MessageId,
    Guid ParticipantId,
    int Ordinal);
