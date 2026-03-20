//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Interception.UI.Application.Observations.Import;

/// <summary>
/// Normalization helpers for raw unknown-person labels.
/// </summary>
internal static partial class UnknownIdentityText
{
    [GeneratedRegex(@"^\s*(нв|nv|unknown|unk)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnknownPrefixRegex();

    [GeneratedRegex(@"^\s*(\?|невідом(ий|а|е)?|unknown|unk)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnknownExactRegex();

    public static bool IsUnknownLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var text = value.Trim();
        return UnknownExactRegex().IsMatch(text) || UnknownPrefixRegex().IsMatch(text);
    }

    public static string? NormalizeRawUnknownLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
