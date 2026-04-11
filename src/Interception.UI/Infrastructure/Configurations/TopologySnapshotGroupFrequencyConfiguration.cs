//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotGroupFrequencyConfiguration : IEntityTypeConfiguration<TopologySnapshotGroupFrequency>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotGroupFrequency> builder)
    {
        builder.ToTable("topology_snapshot_group_frequencies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id")
            .IsRequired();

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_topology_snapshot_group_frequencies_group_id");
    }
}
