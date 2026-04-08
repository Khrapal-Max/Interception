//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class ResolvedParticipantConfiguration
    : IEntityTypeConfiguration<ResolvedParticipant>
{
    public void Configure(EntityTypeBuilder<ResolvedParticipant> builder)
    {
        builder.ToTable("resolved_participants");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(200);

        builder.Property(x => x.Division)
            .HasColumnName("division")
            .HasMaxLength(300);

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(50);

        builder.Property(x => x.ConfirmedBy)
            .HasColumnName("confirmed_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .IsRequired();

        builder.HasIndex(x => new { x.Name, x.Frequency, x.Division })
            .HasDatabaseName("ix_resolved_participants_name_frequency_division");
    }
}
