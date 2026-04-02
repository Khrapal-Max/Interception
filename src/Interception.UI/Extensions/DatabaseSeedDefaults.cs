//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Extensions;

/// <summary>
/// Єдиний довідник seed-даних для portable SQLite-режиму.
/// </summary>
public static class DatabaseSeedDefaults
{
    public static readonly IReadOnlyList<(string Name, string Description)> ActionSeed =
    [
        ("доповідь 200",                  "В примітках вказати РОВ/СОУ"),
        ("доповідь 300",                  "В примітках вказати РОВ/СОУ"),
        ("запит по аеророзвідці",         "Просять дати розвідку"),
        ("запит по забезпеченню",         "Просять ТЗ (вода, батарейки тощо)"),
        ("запит по обстановці",           ""),
        ("запит на ураження",             ""),
        ("запит по зв'язку",              ""),
        ("наказ на аеророзвідку",         ""),
        ("наказ на ураження",             ""),
        ("доповідь по аеророзвідці",      ""),
        ("доповідь по забезпеченню",      ""),
        ("доповідь по обстановці",        "Будь-які дії які не входять в список ДІЇ"),
        ("доповідь по ураженню",          "Примітки РОВ/СОУ"),
        ("доповідь по зв'язку",           ""),
        ("координація переміщення ос",    "Тільки РОВ — якщо СОУ, то це доповідь по обстановці"),
        ("координація дій",               ""),
        ("коригування арт вогню",         ""),
        ("коригування бпла",              ""),
        ("інше",                          "Якщо важко визначити :)")
    ];

    /// <summary>
    /// Синхронізує довідник дій з еталонним списком.
    /// </summary>
    public static async Task EnsureActionSeedAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existing = await db.InterceptionActions
            .ToDictionaryAsync(x => x.Name, x => x, StringComparer.Ordinal, ct);

        var added = 0;
        var updated = 0;

        foreach (var (name, description) in ActionSeed)
        {
            if (existing.TryGetValue(name, out var current))
            {
                var normalizedDescription = description?.Trim() ?? string.Empty;
                if (!string.Equals(current.Description, normalizedDescription, StringComparison.Ordinal))
                {
                    current.Update(name, normalizedDescription);
                    updated++;
                }

                continue;
            }

            db.InterceptionActions.Add(InterceptionAction.Create(name, description));
            added++;
        }

        if (added > 0 || updated > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("Action seed completed. Added: {Added}, Updated: {Updated}", added, updated);
    }
}
