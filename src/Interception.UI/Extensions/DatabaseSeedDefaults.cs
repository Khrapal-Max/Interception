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
        ("взаємодія по місцезнаходженню ос",               ""),
        ("доповідь 200",                                   ""),
        ("доповідь 300",                                   ""),
        ("доповідь 500",                                   ""),
        ("доповідь по аеророзвідці",                       ""),
        ("доповідь по забезпеченню",                       ""),
        ("доповідь по залишкам БК АРТ / ТАНК",             ""),
        ("доповідь по залишкам БК БПС",                    ""),
        ("доповідь по обстановці",                         ""),
        ("доповідь по стану зв'язка",                      ""),
        ("доповідь по ураженню арта",                      ""),
        ("доповідь по ураженню бпла",                      ""),
        ("доповідь про взяття в полон СОУ",                ""),
        ("доповідь про виявлення СОУ",                     ""),
        ("доповідь про вплив (танк / бпак / арт / піхота) СОУ", ""),
        ("доповідь про втрату БПАК РОВ (контроль РЛС)",    ""),
        ("запит на ураження арта",                         ""),
        ("запит на ураження бпла",                         ""),
        ("запит по аеророзвідці",                          ""),
        ("запит по забезпеченню",                          ""),
        ("запит по обстановці",                            ""),
        ("запит по стану зв'язка",                         ""),
        ("інше",                                           ""),
        ("координація дій",                                ""),
        ("координація переміщення ос ров бпак",            ""),
        ("координація переміщення ос ров гармата",         ""),
        ("координація переміщення ос ров евакуація",       ""),
        ("координація переміщення ос ров накат",           ""),
        ("координація переміщення ос ров откат",           ""),
        ("координація переміщення ос ров суміжників",      ""),
        ("координація переміщення ос ров танк",            ""),
        ("координація переміщення ос ров техника (невизначений тип)", ""),
        ("коригування арт вогню",                          ""),
        ("коригування бпла",                               ""),
        ("наказ на аеророзвідку",                          ""),
        ("наказ на реконсцініровку місцевості",            ""),
        ("наказ на ураження арта",                         ""),
        ("наказ на ураження бпла",                         ""),
        ("наказ на ураження танк",                         "")
     ];

    public static readonly IReadOnlyList<(string Name, string Description)> RoleSeed =
    [       
        ("ЗАГАЛЬНЕ ПОВІДОМЛЕННЯ НЕБЕЗПЕКИ",  ""),
        ("командир взвода",                  ""),
        ("водитель",                         ""),
        ("медик",                            ""),
        ("пехота",                           ""),
        ("расчет бпс",                       ""),
        ("расчет артиллерия",                ""),
        ("центр бпс",                        ""),
        ("центр",                            "")
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

    public static async Task EnsureRoleSeedAsync(
       AppDbContext db,
       ILogger logger,
       CancellationToken ct = default)
    {
        var existing = await db.ParticipantRoles
            .ToDictionaryAsync(x => x.Name, x => x, StringComparer.Ordinal, ct);

        var added = 0;
        var updated = 0;

        foreach (var (name, description) in RoleSeed)
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

            db.ParticipantRoles.Add(ParticipantRole.Create(name, description));
            added++;
        }

        if (added > 0 || updated > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("Role seed completed. Added: {Added}, Updated: {Updated}", added, updated);
    }
}
