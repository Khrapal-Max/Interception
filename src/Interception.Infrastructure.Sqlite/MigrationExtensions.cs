//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Interception.Infrastructure.Sqlite;

public static class MigrationExtensions
{
    public static IServiceCollection AddDatabaseStartupMigration(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseMigrationHostedService>();
        return services;
    }

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
        ("інше",                          "Якщо важко визначити :)"),
    ];

    public static async Task AddMigrationDb(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<SqliteDbContext>>()
            .CreateDbContextAsync(ct);

        await db.Database.MigrateAsync(ct);
        logger.LogInformation("✅ SQLite database migrated successfully.");

        await SeedActionsAsync(scope, logger, ct);
    }

    private static async Task SeedActionsAsync(IServiceScope scope, ILogger logger, CancellationToken ct)
    {
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<SqliteDbContext>>()
            .CreateDbContextAsync(ct);

        var existingNames = await db.InterceptionActions
            .Select(a => a.Name)
            .ToHashSetAsync(ct);

        var toAdd = ActionSeed
            .Where(a => !existingNames.Contains(a.Name))
            .Select(a => InterceptionAction.Create(a.Name, a.Description))
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("✅ Actions already seeded, skipping.");
            return;
        }

        await db.InterceptionActions.AddRangeAsync(toAdd, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("✅ Seeded {Count} action(s).", toAdd.Count);
    }
}
