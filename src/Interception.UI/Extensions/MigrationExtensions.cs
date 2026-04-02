//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
// MigrationExtension
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Extensions;

public static class MigrationExtension
{
    private static readonly IReadOnlyList<(string Name, string Description)> ActionSeed =
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

    public static async Task AddMigrationDb(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        if (db.Database.IsSqlite())
        {
            // Для SQLite-гілки bootstrap робимо без залежності від старих PostgreSQL migrations.
            // Якщо в проекті лишився старий ModelSnapshot/Migrations, Migrate() може не створити таблиці.
            await db.Database.EnsureCreatedAsync(ct);
            logger.LogInformation("SQLite schema ensured successfully.");

            await SeedActionsAsync(db, logger, ct);
            return;
        }

        var pending = await db.Database.GetPendingMigrationsAsync(ct);
        if (pending.Any())
        {
            logger.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database migrated successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations.");
        }

        await SeedActionsAsync(db, logger, ct);
    }

    private static async Task SeedActionsAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken ct)
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
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Action seed completed. Added: {Added}, Updated: {Updated}", added, updated);
    }
}
