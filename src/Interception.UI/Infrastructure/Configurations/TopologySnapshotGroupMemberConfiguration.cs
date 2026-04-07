//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotGroupMemberConfiguration : IEntityTypeConfiguration<TopologySnapshotGroupMember>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotGroupMember> builder)
    {
        builder.ToTable("topology_snapshot_group_members");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(300);

        builder.Property(x => x.MentionCount)
            .HasColumnName("mention_count")
            .IsRequired();

        builder.Property(x => x.UniquePartnerCount)
            .HasColumnName("unique_partner_count")
            .IsRequired();

        builder.Property(x => x.ConnectionWeight)
            .HasColumnName("connection_weight")
            .IsRequired();

        builder.Property(x => x.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();

        builder.Property(x => x.GroupCount)
            .HasColumnName("group_count")
            .IsRequired();

        builder.Property(x => x.IsSharedAcrossGroups)
            .HasColumnName("is_shared_across_groups")
            .IsRequired();

        builder.Property(x => x.IsKeyPerson)
            .HasColumnName("is_key_person")
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_topology_snapshot_group_members_group_id");
    }
}
