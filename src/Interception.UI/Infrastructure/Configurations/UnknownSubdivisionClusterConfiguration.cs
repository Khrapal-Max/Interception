//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class UnknownSubdivisionClusterConfiguration : IEntityTypeConfiguration<UnknownSubdivisionCluster>
{
    public void Configure(EntityTypeBuilder<UnknownSubdivisionCluster> b)
    {
        b.ToTable("link_unknown_subdivision_clusters");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.LabelRaw)
            .HasColumnName("label_raw")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.LabelNorm)
            .HasColumnName("label_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.LayerHint)
            .HasColumnName("layer_hint")
            .HasMaxLength(128);

        b.Property(x => x.RmHint)
            .HasColumnName("rm_hint")
            .HasMaxLength(256);

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.ResolvedSubdivisionId)
            .HasColumnName("resolved_subdivision_id");

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        b.Property(x => x.ArchivedAtUtc)
            .HasColumnName("archived_at_utc")
            .HasColumnType("timestamp with time zone");

        b.HasOne(x => x.ResolvedSubdivision)
            .WithMany(x => x.ResolvedClusters)
            .HasForeignKey(x => x.ResolvedSubdivisionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Observations)
            .WithOne(x => x.UnknownSubdivisionCluster)
            .HasForeignKey(x => x.UnknownSubdivisionClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.LabelNorm)
            .HasDatabaseName("ix_link_unknown_subdivision_clusters_label_norm");

        b.HasIndex(x => x.ResolvedSubdivisionId)
            .HasDatabaseName("ix_link_unknown_subdivision_clusters_resolved_subdivision_id");

        b.HasIndex(x => x.ArchivedAtUtc)
            .HasDatabaseName("ix_link_unknown_subdivision_clusters_archived_at_utc");
    }
}
