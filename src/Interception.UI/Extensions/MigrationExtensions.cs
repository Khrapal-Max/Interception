//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------
// MigrationExtension
//-----------------------------------------------------------------------------

using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Extensions;

public static class MigrationExtensions
{
    public static async Task AddMigrationDb(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        var pending = await db.Database.GetPendingMigrationsAsync(ct);
        if (pending.Any())
        {
            /* logger.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pending));
             await db.Database.MigrateAsync(ct);
             logger.LogInformation("Database migrated successfully.");*/

            await db.Database.EnsureCreatedAsync(ct);
        }
        else
        {
            logger.LogInformation("No pending migrations.");
        }

        await DatabaseSeedDefaults.EnsureActionSeedAsync(db, logger, ct);
    }
}
