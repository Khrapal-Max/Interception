//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class TopologySnapshotRunConfiguration : IEntityTypeConfiguration<TopologySnapshotRun>
{
    public void Configure(EntityTypeBuilder<TopologySnapshotRun> builder)
    {
        builder.ToTable("topology_snapshot_runs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.DateFromUtc)
            .HasColumnName("date_from_utc");

        builder.Property(x => x.DateToUtc)
            .HasColumnName("date_to_utc");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IsStale)
            .HasColumnName("is_stale")
            .IsRequired();

        builder.Property(x => x.GroupCount)
            .HasColumnName("group_count")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.HasMany(x => x.Groups)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_topology_snapshot_runs_status");

        builder.HasIndex(x => x.CompletedAt)
            .HasDatabaseName("ix_topology_snapshot_runs_completed_at");

        builder.HasIndex(x => new { x.DateFromUtc, x.DateToUtc, x.Status })
            .HasDatabaseName("ix_topology_snapshot_runs_period_status");
    }
}
