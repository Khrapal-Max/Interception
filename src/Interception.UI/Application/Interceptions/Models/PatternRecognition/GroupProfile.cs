//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.PatternRecognition;

/// <summary>
/// Pivot-профіль observation-набору з частотами, ролями, мітками та парами ознак.
/// </summary>
internal sealed record GroupProfile(
    IReadOnlyDictionary<string, int> Frequencies,
    IReadOnlyDictionary<string, int> VectorSignals,
    IReadOnlyDictionary<string, int> Divisions,
    IReadOnlyDictionary<string, int> Roles,
    IReadOnlyDictionary<string, int> Labels,
    IReadOnlyDictionary<string, int> FrequencyDivisionPairs,
    IReadOnlyDictionary<string, int> VectorDivisionPairs,
    IReadOnlyDictionary<string, int> RoleDivisionPairs,
    IReadOnlyDictionary<string, int> LabelDivisionPairs,
    DateTime WindowStart,
    DateTime WindowEnd,
    string? DominantFrequency,
    string? DominantVector,
    string? DominantDivision,
    string? DominantRole);
