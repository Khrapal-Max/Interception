//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotBridgeConfiguration : IEntityTypeConfiguration<TopologySnapshotBridge>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotBridge> builder)
    {
        builder.ToTable("topology_snapshot_bridges");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id")
            .IsRequired();

        builder.Property(x => x.TargetGroupKey)
            .HasColumnName("target_group_key")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.TargetDivision)
            .HasColumnName("target_division")
            .HasMaxLength(300);

        builder.Property(x => x.ContactPersonName)
            .HasColumnName("contact_person_name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.BridgeFrequency)
            .HasColumnName("bridge_frequency")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Weight)
            .HasColumnName("weight")
            .IsRequired();

        builder.Property(x => x.PrimaryAction)
            .HasColumnName("primary_action")
            .HasMaxLength(300);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasMany(x => x.Actions)
            .WithOne(x => x.Bridge)
            .HasForeignKey(x => x.BridgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_topology_snapshot_bridges_group_id");

        builder.HasIndex(x => new { x.GroupId, x.TargetGroupKey })
            .HasDatabaseName("ix_topology_snapshot_bridges_group_target_group");
    }
}
