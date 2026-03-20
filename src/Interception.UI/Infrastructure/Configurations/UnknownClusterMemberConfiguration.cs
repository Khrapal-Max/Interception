//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class UnknownClusterMemberConfiguration : IEntityTypeConfiguration<UnknownClusterMember>
{
    public void Configure(EntityTypeBuilder<UnknownClusterMember> b)
    {
        b.ToTable("link_unknown_cluster_members");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.UnknownClusterId)
            .HasColumnName("unknown_cluster_id")
            .IsRequired();

        b.Property(x => x.ObservationParticipantId)
            .HasColumnName("observation_participant_id")
            .IsRequired();

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.HasOne(x => x.UnknownCluster)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.UnknownClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ObservationParticipant)
            .WithMany()
            .HasForeignKey(x => x.ObservationParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UnknownClusterId)
            .HasDatabaseName("ix_link_unknown_cluster_members_unknown_cluster_id");

        b.HasIndex(x => x.ObservationParticipantId)
            .IsUnique()
            .HasDatabaseName("ux_link_unknown_cluster_members_observation_participant_id");

        b.HasIndex(x => new { x.UnknownClusterId, x.ObservationParticipantId })
            .IsUnique()
            .HasDatabaseName("ux_link_unknown_cluster_members_cluster_participant");
    }
}
