//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Topology;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotBridgeActionConfiguration : IEntityTypeConfiguration<TopologySnapshotBridgeAction>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotBridgeAction> builder)
    {
        builder.ToTable("topology_snapshot_bridge_actions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.BridgeId)
            .HasColumnName("bridge_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .HasColumnName("is_primary")
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(x => x.BridgeId)
            .HasDatabaseName("ix_topology_snapshot_bridge_actions_bridge_id");
    }
}
