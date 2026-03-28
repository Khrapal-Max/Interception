//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.PatternRecognition;

/// <summary>
/// Коренева модель карти зв'язків.
/// </summary>
public sealed record LinkMapModel(
    IReadOnlyList<LinkMapNodeModel> Nodes,
    IReadOnlyList<LinkMapEdgeModel> Edges);

/// <summary>
/// Вузол карти зв'язків.
/// </summary>
public sealed record LinkMapNodeModel(
    string PersonKey,
    string Name,
    string? Role,
    IReadOnlyList<string> Divisions,
    IReadOnlyList<string> Frequencies,
    int ConnectionCount,
    bool IsLocalCenterCandidate,
    bool IsBridgeCandidate,
    bool IsMultiDivisionCandidate);

/// <summary>
/// Зв'язок між двома вузлами.
/// </summary>
public sealed record LinkMapEdgeModel(
    string FromPersonKey,
    string ToPersonKey,
    int Weight,
    IReadOnlyList<string> Divisions,
    IReadOnlyList<string> Frequencies,
    IReadOnlyList<string> Labels);
