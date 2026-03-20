//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ObservationProbableActionConfiguration : IEntityTypeConfiguration<ObservationProbableAction>
{
    public void Configure(EntityTypeBuilder<ObservationProbableAction> b)
    {
        b.ToTable("link_observation_probable_actions", tb =>
        {
            tb.HasCheckConstraint("ck_link_observation_probable_actions_confidence", "confidence >= 0 and confidence <= 1");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.ObservationId)
            .HasColumnName("observation_id")
            .IsRequired();

        b.Property(x => x.ObservationActionId)
            .HasColumnName("observation_action_id")
            .IsRequired();

        b.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(5, 4)
            .IsRequired();

        b.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1024);

        b.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        b.HasOne(x => x.Observation)
            .WithMany(x => x.ProbableActions)
            .HasForeignKey(x => x.ObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ObservationAction)
            .WithMany(x => x.ProbableActions)
            .HasForeignKey(x => x.ObservationActionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ObservationId)
            .HasDatabaseName("ix_link_observation_probable_actions_observation_id");

        b.HasIndex(x => x.ObservationActionId)
            .HasDatabaseName("ix_link_observation_probable_actions_observation_action_id");

        b.HasIndex(x => new { x.ObservationId, x.ObservationActionId })
            .IsUnique()
            .HasDatabaseName("ux_link_observation_probable_actions_observation_action");
    }
}
