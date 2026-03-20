//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class UnknownSubdivisionObservationConfiguration : IEntityTypeConfiguration<UnknownSubdivisionObservation>
{
    public void Configure(EntityTypeBuilder<UnknownSubdivisionObservation> b)
    {
        b.ToTable("link_unknown_subdivision_observations");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.UnknownSubdivisionClusterId)
            .HasColumnName("unknown_subdivision_cluster_id")
            .IsRequired();

        b.Property(x => x.ObservationId)
            .HasColumnName("observation_id")
            .IsRequired();

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.HasOne(x => x.UnknownSubdivisionCluster)
            .WithMany(x => x.Observations)
            .HasForeignKey(x => x.UnknownSubdivisionClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Observation)
            .WithMany()
            .HasForeignKey(x => x.ObservationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.UnknownSubdivisionClusterId)
            .HasDatabaseName("ix_link_unknown_subdivision_observations_cluster_id");

        b.HasIndex(x => x.ObservationId)
            .HasDatabaseName("ix_link_unknown_subdivision_observations_observation_id");

        b.HasIndex(x => new { x.UnknownSubdivisionClusterId, x.ObservationId })
            .IsUnique()
            .HasDatabaseName("ux_link_unknown_subdivision_observations_cluster_observation");
    }
}
