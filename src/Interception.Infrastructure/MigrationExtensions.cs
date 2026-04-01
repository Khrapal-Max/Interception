//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
// MigrationExtension
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Interception.Infrastructure;

public static class MigrationExtensions
{
    public static IServiceCollection AddDatabaseStartupMigration(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseMigrationHostedService>();
        return services;
    }

    // -------------------------------------------------------------------------
    // Еталонний довідник дій
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Міграція + сід
    // -------------------------------------------------------------------------

    public static async Task AddMigrationDb(this IServiceProvider services, CancellationToken ct = default)
    {
        const int maxRetries = 10;
        var delay = TimeSpan.FromSeconds(2);

        Exception? lastError = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Startup");

            try
            {
                await using var db = await scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<AppDbContext>>()
                    .CreateDbContextAsync(ct);
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("✅ Database migrated successfully.");

                // Сід довідника дій — виконується після кожного старту,
                // SeedActionsAsync пропускає вже існуючі назви
                await SeedActionsAsync(scope, logger, ct);

                return;
            }
            catch (PostgresException ex) when (IsMissingDatabase(ex))
            {
                lastError = ex;

                // Try create DB, then retry
                try
                {
                    await EnsureDatabaseExistsAsync(scope, logger, maintenanceDatabase: "postgres", ct);
                    logger.LogInformation("✅ Database created. Retrying migrations...");
                }
                catch (Exception createEx)
                {
                    lastError = createEx;
                    logger.LogWarning(createEx, "Failed to create database.");
                }

                logger.LogWarning(ex, "DB missing (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
            }
            catch (Exception ex) when (ex is PostgresException || ex is NpgsqlException)
            {
                lastError = ex;
                logger.LogWarning(ex, "DB migrate failed (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.LogWarning(ex, "Unexpected migrate error (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(delay, ct);
                delay += TimeSpan.FromSeconds(1);
            }
        }

        throw new InvalidOperationException(
            $"Database migration failed after {maxRetries} attempts.",
            lastError ?? new Exception("Unknown migration error"));
    }

    // -------------------------------------------------------------------------
    // Сід дій
    // -------------------------------------------------------------------------

    private static async Task SeedActionsAsync(
        IServiceScope scope,
        ILogger logger,
        CancellationToken ct)
    {
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync(ct);

        var existingNames = await db.InterceptionActions
            .Select(a => a.Name)
            .ToHashSetAsync(ct);

        var toAdd = ActionSeed
            .Where(a => !existingNames.Contains(a.Name))
            .Select(a => new InterceptionAction
            {
                Name = a.Name,
                Description = a.Description
            })
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsMissingDatabase(PostgresException ex)
        => ex.SqlState == "3D000" // invalid_catalog_name (database does not exist)
           || ex.MessageText.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureDatabaseExistsAsync(
        IServiceScope scope,
        ILogger logger,
        string maintenanceDatabase,
        CancellationToken ct)
    {
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync(ct);
        var connStr = db.Database.GetDbConnection().ConnectionString;

        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Connection string is empty.");

        var csb = new NpgsqlConnectionStringBuilder(connStr);
        var targetDb = csb.Database;

        if (string.IsNullOrWhiteSpace(targetDb))
            throw new InvalidOperationException("Target database name is empty in connection string.");

        // Connect to maintenance DB
        var adminCsb = new NpgsqlConnectionStringBuilder(connStr)
        {
            Database = maintenanceDatabase,
            Pooling = false
        };

        await using var conn = new NpgsqlConnection(adminCsb.ConnectionString);
        await conn.OpenAsync(ct);

        // Check DB exists
        await using (var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name;", conn))
        {
            check.Parameters.AddWithValue("name", targetDb);
            var exists = await check.ExecuteScalarAsync(ct) is not null;

            if (exists)
            {
                logger.LogInformation("Database exists: {Database}.", targetDb);
                return;
            }
        }

        // Create DB
        var sql = $@"CREATE DATABASE ""{targetDb.Replace("\"", "\"\"")}"";";
        await using var create = new NpgsqlCommand(sql, conn);
        await create.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Created database: {Database}.", targetDb);
    }
}
