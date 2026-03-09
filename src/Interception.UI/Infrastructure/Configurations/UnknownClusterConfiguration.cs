//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

/// <summary>
/// Конфігурація кластера невизначених осіб.
/// </summary>
public sealed class UnknownClusterConfiguration : IEntityTypeConfiguration<UnknownCluster>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UnknownCluster> b)
    {
        b.ToTable("link_unknown_clusters", tb =>
        {
            tb.HasCheckConstraint("ck_link_unknown_clusters_status", "status in ('open', 'resolved', 'merged', 'archived')");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(256);

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(x => x.ResolvedActorId)
            .HasColumnName("resolved_actor_id");

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        b.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_link_unknown_clusters_code");

        b.HasIndex(x => x.ResolvedActorId)
            .HasDatabaseName("ix_link_unknown_clusters_resolved_actor_id");

        b.HasOne(x => x.ResolvedActor)
            .WithMany(x => x.UnknownClusters)
            .HasForeignKey(x => x.ResolvedActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
