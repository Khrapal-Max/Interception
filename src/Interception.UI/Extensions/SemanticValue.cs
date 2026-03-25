//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Extensions;

/// <summary>
/// Нормалізація значень для аналітики.
/// "НВ підрозділ", "невідома", "unknown" тощо не повинні попадати в pivot-логіку
/// як справжні роль / підрозділ / значення ознаки.
/// </summary>
public static class SemanticValue
{
    private static readonly HashSet<string> UnknownTokens =
    [
        "-",
        "—",
        "НВ",
        "Н/В",
        "НЕВІДОМА",
        "НЕВІДОМИЙ",
        "НЕВІДОМЕ",
        "НЕИЗВЕСТНАЯ",
        "НЕИЗВЕСТНЫЙ",
        "НЕИЗВЕСТНОЕ",
        "UNKNOWN",
        "НВ ПІДРОЗДІЛ",
        "НЕВІДОМИЙ ПІДРОЗДІЛ",
        "НЕВІДОМА РОЛЬ",
        "НЕИЗВЕСТНОЕ ПОДРАЗДЕЛЕНИЕ",
        "НЕИЗВЕСТНАЯ РОЛЬ"
    ];

    public static string? NormalizeMeaningfulOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return UnknownTokens.Contains(trimmed.ToUpperInvariant())
            ? null
            : trimmed;
    }

    public static string? NormalizeKeyOrNull(string? value)
        => NormalizeMeaningfulOrNull(value)?.ToUpperInvariant();

    public static bool IsMeaningful(string? value)
        => NormalizeMeaningfulOrNull(value) is not null;
}
