//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.ValueObjects;

/// <summary>
/// Value Object для ролі учасника.
/// </summary>
public readonly record struct RoleName
{
    public string Value { get; }

    private RoleName(string value)
        => Value = value;

    public static RoleName? Create(string? value)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : new RoleName(normalized);
    }

    public override string ToString() => Value;
}
