//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ObservationParticipantConfiguration : IEntityTypeConfiguration<ObservationParticipant>
{
    public void Configure(EntityTypeBuilder<ObservationParticipant> b)
    {
        b.ToTable("link_observation_participants", td =>
        {
            // Checks
            td.HasCheckConstraint("ck_link_obs_participants_ordinal", "ordinal >= 1");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.ObservationId)
            .HasColumnName("observation_id")
            .IsRequired();

        b.Property(x => x.LabelRaw)
            .HasColumnName("label_raw")
            .HasMaxLength(128);

        b.Property(x => x.LabelNorm)
            .HasColumnName("label_norm")
            .HasMaxLength(128);

        b.Property(x => x.IsUnknown)
            .HasColumnName("is_unknown")
            .IsRequired();

        b.Property(x => x.RoleRaw)
            .HasColumnName("role_raw")
            .HasMaxLength(128);

        b.Property(x => x.Ordinal)
            .HasColumnName("ordinal")
            .IsRequired();

        // Indexes
        b.HasIndex(x => x.ObservationId)
            .HasDatabaseName("ix_link_obs_participants_observation_id");

        b.HasIndex(x => x.LabelNorm)
            .HasDatabaseName("ix_link_obs_participants_label_norm");

        // Унікальність учасника в межах одного observation — тільки для відомих (label_norm != null)
        b.HasIndex(x => new { x.ObservationId, x.LabelNorm })
            .IsUnique()
            .HasFilter("label_norm is not null")
            .HasDatabaseName("ux_link_obs_participants_observation_label_norm");
    }
}