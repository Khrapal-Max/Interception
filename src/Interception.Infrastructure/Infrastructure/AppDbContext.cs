//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InterceptionAction> InterceptionActions { get; init; }
    public DbSet<InterceptionMessage> InterceptionMessages { get; init; }
    public DbSet<InterceptionMessageLabel> InterceptionMessageLabels { get; init; }
    public DbSet<InterceptionMessageParticipant> InterceptionMessageParticipants { get; init; }

    public DbSet<ParticipantCandidateGroup> ParticipantCandidateGroups { get; init; }
    public DbSet<ResolvedParticipant> ResolvedParticipants { get; init; }

    public DbSet<DailyReport> DailyReports { get; init; }
    public DbSet<MessageGroup> MessageGroups { get; init; }
    public DbSet<ParticipantMatrix> ParticipantMatrices { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.IsNpgsql())
            modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
