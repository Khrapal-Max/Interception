//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Builders.LinkMap;

internal sealed record BridgeAccumulator(
    string TargetGroupKey,
    string? TargetDivision,
    string ContactPersonName,
    string BridgeFrequency,
    int Weight,
    string? PrimaryAction,
    IReadOnlyList<string> TopActions);
