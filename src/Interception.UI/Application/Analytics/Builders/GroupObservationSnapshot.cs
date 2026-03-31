//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Builders;

/// <summary>
/// Зведений observation-зріз для аналізу перехресть у групі або контексті.
/// </summary>
internal sealed record GroupObservationSnapshot(
    string? Frequency,
    string? VectorSignal,
    string? Division,
    string? Role,
    DateTime ObservedDate,
    List<string> Labels);
