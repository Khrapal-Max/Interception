//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Analytics.Topologies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotGroupConfiguration : IEntityTypeConfiguration<TopologySnapshotGroup>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotGroup> builder)
    {
        builder.ToTable("topology_snapshot_groups");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Division)
            .HasColumnName("division")
            .HasMaxLength(300);

        builder.Property(x => x.KeyPersonName)
            .HasColumnName("key_person_name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.KeyPersonRole)
            .HasColumnName("key_person_role")
            .HasMaxLength(300);

        builder.Property(x => x.MentionCount)
            .HasColumnName("mention_count")
            .IsRequired();

        builder.Property(x => x.InternalConnectionWeight)
            .HasColumnName("internal_connection_weight")
            .IsRequired();

        builder.Property(x => x.BridgeWeight)
            .HasColumnName("bridge_weight")
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasMany(x => x.Frequencies)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Actions)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Bridges)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RunId)
            .HasDatabaseName("ix_topology_snapshot_groups_run_id");

        builder.HasIndex(x => new { x.RunId, x.GroupKey })
            .HasDatabaseName("ix_topology_snapshot_groups_run_group_key");
    }
}
