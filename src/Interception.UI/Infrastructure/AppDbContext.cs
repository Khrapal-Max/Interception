//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Observation> Observations => Set<Observation>();
    public DbSet<ObservationParticipant> ObservationParticipants => Set<ObservationParticipant>();
    public DbSet<ObservationAction> ObservationActions => Set<ObservationAction>();
    public DbSet<ObservationTag> ObservationTags => Set<ObservationTag>();
    public DbSet<TagCatalog> TagCatalogs => Set<TagCatalog>();
    public DbSet<ObservationProbableAction> ObservationProbableActions => Set<ObservationProbableAction>();
    public DbSet<UnknownCluster> UnknownClusters => Set<UnknownCluster>();
    public DbSet<UnknownClusterMember> UnknownClusterMembers => Set<UnknownClusterMember>();
    public DbSet<ResolvedActor> ResolvedActors => Set<ResolvedActor>();
    public DbSet<UnknownSubdivisionCluster> UnknownSubdivisionClusters => Set<UnknownSubdivisionCluster>();
    public DbSet<UnknownSubdivisionObservation> UnknownSubdivisionObservations => Set<UnknownSubdivisionObservation>();
    public DbSet<ResolvedSubdivision> ResolvedSubdivisions => Set<ResolvedSubdivision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("btree_gist");
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
