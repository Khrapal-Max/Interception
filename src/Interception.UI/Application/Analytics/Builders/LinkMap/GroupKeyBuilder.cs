//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Builders.LinkMap;

internal static class GroupKeyBuilder
{
    private const string NoDivision = "NO-DIVISION";

    public static string BuildFrequencyScopedGroupKey(
        string frequency,
        string? division,
        IEnumerable<string> members,
        string keyPersonName)
    {
        var stablePart = BuildStableGroupKey(division, members, keyPersonName);
        return $"{frequency.Trim().ToUpperInvariant()}::{stablePart}";
    }

    public static string BuildStableGroupKey(string? division, IEnumerable<string> members, string keyPersonName)
    {
        var divisionPart = string.IsNullOrWhiteSpace(division) ? NoDivision : division.Trim().ToUpperInvariant();
        var membersPart = string.Join(";", members
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        return $"{divisionPart}|{keyPersonName.Trim().ToUpperInvariant()}|{membersPart}";
    }
}
