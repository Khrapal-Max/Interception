//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

/// <summary>
/// Конфігурація прив'язки raw-учасників до unknown-кластерів.
/// </summary>
public sealed class UnknownClusterMemberConfiguration : IEntityTypeConfiguration<UnknownClusterMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UnknownClusterMember> b)
    {
        b.ToTable("link_unknown_cluster_members", tb =>
        {
            tb.HasCheckConstraint("ck_link_unknown_cluster_members_confidence", "confidence is null or (confidence >= 0 and confidence <= 1)");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.UnknownClusterId)
            .HasColumnName("unknown_cluster_id")
            .IsRequired();

        b.Property(x => x.ObservationParticipantId)
            .HasColumnName("observation_participant_id")
            .IsRequired();

        b.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(5, 4);

        b.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(512);

        b.Property(x => x.AddedAtUtc)
            .HasColumnName("added_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.AddedBy)
            .HasColumnName("added_by")
            .HasMaxLength(128);

        b.HasIndex(x => x.UnknownClusterId)
            .HasDatabaseName("ix_link_unknown_cluster_members_cluster_id");

        b.HasIndex(x => x.ObservationParticipantId)
            .IsUnique()
            .HasDatabaseName("ux_link_unknown_cluster_members_participant_id");

        b.HasOne(x => x.UnknownCluster)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.UnknownClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ObservationParticipant)
            .WithMany()
            .HasForeignKey(x => x.ObservationParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
