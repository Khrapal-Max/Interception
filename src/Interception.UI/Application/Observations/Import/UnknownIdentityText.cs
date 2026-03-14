//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Interception.UI.Application.Observations.Import;

/// <summary>
/// Допоміжна логіка для розпізнавання невідомих осіб у raw-імпорті.
/// </summary>
internal static partial class UnknownIdentityText
{
    private static readonly Regex UnknownPrefixRegex = UnknownPrefix();

    private static readonly Regex UnknownExactRegex = UnknownExact();

    public static bool IsUnknownLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var text = value.Trim();

        if (UnknownExactRegex.IsMatch(text))
            return true;

        return UnknownPrefixRegex.IsMatch(text);
    }

    public static string? NormalizeRawUnknownLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        return IsUnknownLabel(text) ? text : text;
    }

    public static string BuildUnknownDisplay(string? labelRaw, int ordinal, DateOnly? observedDate = null, string? rmRaw = null)
    {
        if (!string.IsNullOrWhiteSpace(labelRaw))
            return labelRaw.Trim();

        if (!string.IsNullOrWhiteSpace(rmRaw) && observedDate is not null)
            return $"НВ {ordinal} · {observedDate:dd.MM} · {rmRaw}";

        if (observedDate is not null)
            return $"НВ {ordinal} · {observedDate:dd.MM}";

        return $"НВ {ordinal}";
    }

    [GeneratedRegex(@"^\s*(нв|nv|unknown|unk)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex UnknownPrefix();
    [GeneratedRegex(@"^\s*(\?|невідом(ий|а|е)?|unknown|unk)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex UnknownExact();
}