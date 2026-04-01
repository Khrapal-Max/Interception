//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests;

internal static class TestDbFactory
{
    public static IDbContextFactory<AppDbContext> CreateFactory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString("N");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;

        return new TestAppDbContextFactory(options);
    }

    private sealed class TestAppDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options = options;

        public AppDbContext CreateDbContext() => new(_options);
    }
}
