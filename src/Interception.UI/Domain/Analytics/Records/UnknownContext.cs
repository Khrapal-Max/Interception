//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Analytics.Records;

/// <summary>
/// Плоский контекст невідомого учасника без EF-навігацій.
/// Використовується в domain service/specification для групування кандидатів.
/// </summary>
public sealed record UnknownContext(
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
