//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.ValueObjects;

/// <summary>
/// Value Object для імені/позивного особи з єдиною нормалізацією.
/// </summary>
public readonly record struct PersonName
{
    /// <summary>Нормалізоване значення для відображення.</summary>
    public string Value { get; }

    private PersonName(string value)
        => Value = value;

    /// <summary>
    /// Створює VO з довільного вхідного рядка.
    /// </summary>
    public static PersonName? Create(string? value)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : new PersonName(normalized);
    }

    /// <summary>
    /// Ключ для порівняння/пошуку.
    /// </summary>
    public string ToLookupKey()
        => Value.Trim().ToUpperInvariant();

    public override string ToString() => Value;
}
