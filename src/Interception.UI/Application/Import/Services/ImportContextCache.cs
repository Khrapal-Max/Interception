//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Interceptions;
using Interception.UI.Extensions;

namespace Interception.UI.Application.Import.Services;

/// <summary>
/// Короткочасний кеш для одного сеансу імпорту.
/// Працює з довідником дій і ролей.
/// </summary>
public sealed class ImportContextCache(
    IEnumerable<InterceptionAction> actions,
    IReadOnlyDictionary<string, string> roleMap)
{
    private readonly Dictionary<string, InterceptionAction> _actions =
        actions.ToDictionary(a => NormalizeKey(a.Name), a => a, StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, string> _roleMap = roleMap;

    public InterceptionAction? FindAction(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        _actions.TryGetValue(NormalizeKey(name), out var action);
        return action;
    }

    public string? ResolveRole(string? role)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(role);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return _roleMap.TryGetValue(NormalizeKey(normalized), out var catalogRole)
            ? catalogRole
            : normalized;
    }

    private static string NormalizeKey(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value)?.Trim().ToUpperInvariant() ?? string.Empty;
}
