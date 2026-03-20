//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ObservationActionConfiguration : IEntityTypeConfiguration<ObservationAction>
{
    public void Configure(EntityTypeBuilder<ObservationAction> b)
    {
        b.ToTable("link_observation_actions", tb =>
        {
            tb.HasCheckConstraint("ck_link_observation_actions_typical_participants_count", "typical_participants_count is null or typical_participants_count >= 1");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.NameNorm)
            .HasColumnName("name_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.InitiatorRoleName)
            .HasColumnName("initiator_role_name")
            .HasMaxLength(128);

        b.Property(x => x.ResponderRoleName)
            .HasColumnName("responder_role_name")
            .HasMaxLength(128);

        b.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1024);

        b.Property(x => x.TypicalParticipantsCount)
            .HasColumnName("typical_participants_count");

        b.Property(x => x.RequiresCounterparty)
            .HasColumnName("requires_counterparty")
            .IsRequired();

        b.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        b.HasIndex(x => x.NameNorm)
            .IsUnique()
            .HasDatabaseName("ux_link_observation_actions_name_norm");

        b.HasIndex(x => new { x.IsActive, x.NameNorm })
            .HasDatabaseName("ix_link_observation_actions_active_name_norm");

        b.HasIndex(x => x.Category)
            .HasDatabaseName("ix_link_observation_actions_category");
    }
}
