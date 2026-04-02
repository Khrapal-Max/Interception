//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests;

internal static class TestDbFactory
{
    public static IDbContextFactory<PostgreSqlDbContext> CreateFactory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString("N");

        var options = new DbContextOptionsBuilder<PostgreSqlDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;

        return new TestAppDbContextFactory(options);
    }

    private sealed class TestAppDbContextFactory(DbContextOptions<PostgreSqlDbContext> options) : IDbContextFactory<PostgreSqlDbContext>
    {
        private readonly DbContextOptions<PostgreSqlDbContext> _options = options;

        public PostgreSqlDbContext CreateDbContext() => new(_options);
    }
}
