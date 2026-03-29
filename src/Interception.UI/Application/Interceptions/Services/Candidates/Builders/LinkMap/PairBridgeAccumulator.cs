//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Services.Candidates.Builders.LinkMap;

internal sealed class PairBridgeAccumulator(string leftGroupKey, string rightGroupKey)
{
    private readonly Dictionary<string, int> _actionCounts = new(StringComparer.OrdinalIgnoreCase);

    public string LeftGroupKey { get; } = leftGroupKey;
    public string RightGroupKey { get; } = rightGroupKey;
    public int TotalWeight { get; set; }
    public Dictionary<string, FrequencyBridgeAccumulator> ByFrequency { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return;

        var key = actionName.Trim();
        _actionCounts[key] = _actionCounts.TryGetValue(key, out var current)
            ? current + 1
            : 1;
    }

    public string? GetPrimaryAction()
    {
        return _actionCounts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();
    }

    public IReadOnlyList<string> GetTopActions(int take = 3)
    {
        take = Math.Clamp(take, 1, 10);
        return _actionCounts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(take)
            .Select(x => x.Key)
            .ToList();
    }
}
