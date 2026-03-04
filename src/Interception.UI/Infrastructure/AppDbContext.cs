//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Postgres розширення для темпоральних обмежень
        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("btree_gist");
        }

        // Застосувати всі конфігурації з поточної збірки
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
