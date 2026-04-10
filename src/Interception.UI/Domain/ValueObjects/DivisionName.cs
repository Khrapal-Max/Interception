//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.ValueObjects;

/// <summary>
/// Value Object для назви підрозділу/напрямку.
/// </summary>
public readonly record struct DivisionName
{
    /// <summary>Нормалізоване значення підрозділу.</summary>
    public string Value { get; }

    private DivisionName(string value)
        => Value = value;

    /// <summary>Створює VO з довільного вхідного рядка.</summary>
    public static DivisionName? Create(string? value)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : new DivisionName(normalized);
    }

    public override string ToString() => Value;
}
