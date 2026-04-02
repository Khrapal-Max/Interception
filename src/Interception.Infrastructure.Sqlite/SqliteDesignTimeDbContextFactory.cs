//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Interception.Infrastructure.Sqlite;

public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SqliteDbContext>();
        builder.UseSqlite("Data Source=interception.desktop.db");
        return new SqliteDbContext(builder.Options);
    }
}
