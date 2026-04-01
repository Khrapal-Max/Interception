//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
// MigrationExtension
//-----------------------------------------------------------------------------

using Interception.Application.Registry.Abstractions;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Interception.Infrastructure.Extensions;

public static class MigrationExtension
{
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

    public static async Task AddMigrationDb(this WebApplication app, CancellationToken ct = default)
    {
        const int maxRetries = 10;
        var delay = TimeSpan.FromSeconds(2);

        Exception? lastError = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Startup");

            // Support both AddDbContextFactory<AppDbContext>() and AddDbContext<AppDbContext>()
            var factory = scope.ServiceProvider.GetService<IDbContextFactory<AppDbContext>>();

            AppDbContext db;
            IAsyncDisposable? dbToDispose = null;

            try
            {
                db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // If DB doesn't exist, this may throw 3D000; handle below
                var pending = await db.Database.GetPendingMigrationsAsync(ct);

                if (pending.Any())
                {
                    logger.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pending));
                    await db.Database.MigrateAsync(ct);
                    logger.LogInformation("✅ Database migrated successfully.");
                }
                else
                {
                    logger.LogInformation("✅ No pending migrations.");
                }

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
            finally
            {
                if (dbToDispose is not null)
                    await dbToDispose.DisposeAsync();
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
        var actionService = scope.ServiceProvider
            .GetRequiredService<IInterceptionActionService>();

        var added = 0;
        foreach (var (name, description) in ActionSeed)
        {
            try
            {
                await actionService.CreateAsync(name, description, ct);
                added++;
            }
            catch (InvalidOperationException)
            {
                // Вже існує — пропускаємо
            }
        }

        if (added > 0)
            logger.LogInformation("✅ Seeded {Count} action(s).", added);
        else
            logger.LogInformation("✅ Actions already seeded, skipping.");
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
        // Read connection string from the context registration
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<AppDbContext>>();
        string? connStr;

        if (factory is not null)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            connStr = db.Database.GetDbConnection().ConnectionString;
        }
        else
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            connStr = db.Database.GetDbConnection().ConnectionString;
        }

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
