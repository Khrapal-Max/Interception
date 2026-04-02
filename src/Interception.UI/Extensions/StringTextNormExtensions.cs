//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Interception.UI.Extensions;

public static partial class StringTextNormExtensions
{
    private static readonly Regex MultiWs = MyRegex();

    public static string NormalizeRequired(string value)
        => NormalizeToLowerInvariant(value) ?? throw new ArgumentException("Value must not be empty.", nameof(value));

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    public static string? NormalizeToLowerInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Trim, collapse whitespace, and lower-case for grouping/searching
        var trimmed = MultiWs.Replace(value.Trim(), " ");
        return trimmed.ToLowerInvariant();
    }

    public static string? NormalizeOption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Trim, collapse whitespace, and lower-case for grouping/searching
        return MultiWs.Replace(value.Trim(), " ");
    }
}
