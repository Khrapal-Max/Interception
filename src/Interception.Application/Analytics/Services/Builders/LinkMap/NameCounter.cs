//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Services.Builders.LinkMap;

internal sealed class NameCounter
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public void Increment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var key = name.Trim();
        _counts[key] = _counts.TryGetValue(key, out var current) ? current + 1 : 1;
    }

    public string? GetTopName()
    {
        return _counts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();
    }
}
