//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Services.Candidates.Builders.LinkMap;

internal sealed class PairBridgeAccumulator(string leftGroupKey, string rightGroupKey)
{
    public string LeftGroupKey { get; } = leftGroupKey;
    public string RightGroupKey { get; } = rightGroupKey;
    public int TotalWeight { get; set; }
    public Dictionary<string, FrequencyBridgeAccumulator> ByFrequency { get; } = new(StringComparer.OrdinalIgnoreCase);
}
