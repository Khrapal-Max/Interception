//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Registry.Services;

internal static class ParticipantRoleCatalogSupport
{
    public static async Task<Dictionary<string, string>> LoadRoleMapAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.ParticipantRoles
            .AsNoTracking()
            .Select(x => x.Name)
            .ToListAsync(ct);

        return rows
            .Select(x => new
            {
                Key = NormalizeKey(x),
                Value = x.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    public static string? NormalizeRole(string? role, IReadOnlyDictionary<string, string> roleMap)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(role);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var key = NormalizeKey(normalized);
        return roleMap.TryGetValue(key, out var catalogRole)
            ? catalogRole
            : normalized;
    }

    private static string NormalizeKey(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value)?.Trim().ToUpperInvariant() ?? string.Empty;
}
