//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interception.Infrastructure;

/// <summary>
/// Runs EF Core migrations and seed on host startup.
/// </summary>
public sealed class DatabaseMigrationHostedService(
    IServiceProvider services,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting database migration hosted service...");
        await services.AddMigrationDb(cancellationToken);
        logger.LogInformation("Database migration hosted service finished.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
