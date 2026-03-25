//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.PatternRecognition;

/// <summary>
/// Плоский контекст невідомого учасника без EF-навігацій.
/// Використовується як вхідна модель для аналізу груп кандидатів.
/// </summary>
internal sealed record UnknownContext(
    Guid ParticipantId,
    int Ordinal,
    Guid MessageId,
    string? Frequency,
    string? VectorSignal,
    string? PointSignal,
    string? Division,
    string? Role,
    DateTime ObservedDate,
    HashSet<string> KnownPartnerNames,
    HashSet<string> Labels);
