//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Builders;

/// <summary>
/// Знімок повідомлення для enrich/read-side сценаріїв без EF-навігацій.
/// </summary>
internal sealed record MessageSnapshot(
    Guid Id,
    DateTime ObservedDate,
    string? Frequency,
    string? VectorSignal,
    string? Division);
