//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.Infrastructure.Sqlite.Configurations;

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

        builder.Property(x => x.ConfirmedBy)
            .HasColumnName("confirmed_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .IsRequired();

        // Ім'я унікальне — одна встановлена особа з таким позивним
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("ix_resolved_participants_name");
    }
}
