//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.ValueObjects;

/// <summary>
/// Value Object для частоти у нормалізованому вигляді.
/// </summary>
public readonly record struct FrequencyCode
{
    /// <summary>Нормалізоване значення частоти.</summary>
    public string Value { get; }

    private FrequencyCode(string value)
        => Value = value;

    /// <summary>Створює VO з довільного вхідного рядка.</summary>
    public static FrequencyCode? Create(string? value)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : new FrequencyCode(normalized);
    }

    public override string ToString() => Value;
}
