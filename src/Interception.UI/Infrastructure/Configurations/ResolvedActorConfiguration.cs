//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ResolvedActorConfiguration : IEntityTypeConfiguration<ResolvedActor>
{
    public void Configure(EntityTypeBuilder<ResolvedActor> b)
    {
        b.ToTable("link_resolved_actors");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.DisplayNameNorm)
            .HasColumnName("display_name_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.PrimaryRole)
            .HasColumnName("primary_role")
            .HasMaxLength(128);

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

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

        b.HasIndex(x => x.DisplayNameNorm)
            .HasDatabaseName("ix_link_resolved_actors_display_name_norm");

        b.HasIndex(x => new { x.IsActive, x.DisplayNameNorm })
            .HasDatabaseName("ix_link_resolved_actors_active_display_name_norm");
    }
}
