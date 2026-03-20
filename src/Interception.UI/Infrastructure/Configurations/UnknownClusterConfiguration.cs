//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class UnknownClusterConfiguration : IEntityTypeConfiguration<UnknownCluster>
{
    public void Configure(EntityTypeBuilder<UnknownCluster> b)
    {
        b.ToTable("link_unknown_clusters");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.TitleNorm)
            .HasColumnName("title_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.ResolvedActorId)
            .HasColumnName("resolved_actor_id");

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

        b.HasOne(x => x.ResolvedActor)
            .WithMany(x => x.ResolvedClusters)
            .HasForeignKey(x => x.ResolvedActorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Members)
            .WithOne(x => x.UnknownCluster)
            .HasForeignKey(x => x.UnknownClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TitleNorm)
            .HasDatabaseName("ix_link_unknown_clusters_title_norm");

        b.HasIndex(x => x.ResolvedActorId)
            .HasDatabaseName("ix_link_unknown_clusters_resolved_actor_id");

        b.HasIndex(x => x.ArchivedAtUtc)
            .HasDatabaseName("ix_link_unknown_clusters_archived_at_utc");
    }
}
