//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interception.Infrastructure.Sqlite;

/// <summary>
/// Runs EF Core migrations and seed on host startup.
/// </summary>
public sealed class DatabaseMigrationHostedService(
    IServiceProvider services,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting SQLite database migration hosted service...");
        await services.AddMigrationDb(cancellationToken);
        logger.LogInformation("SQLite database migration hosted service finished.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
