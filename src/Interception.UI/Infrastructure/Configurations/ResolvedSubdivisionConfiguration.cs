//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ResolvedSubdivisionConfiguration : IEntityTypeConfiguration<ResolvedSubdivision>
{
    public void Configure(EntityTypeBuilder<ResolvedSubdivision> b)
    {
        b.ToTable("link_resolved_subdivisions");

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

        b.Property(x => x.LayerHint)
            .HasColumnName("layer_hint")
            .HasMaxLength(128);

        b.Property(x => x.RmHint)
            .HasColumnName("rm_hint")
            .HasMaxLength(256);

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

        b.HasIndex(x => x.NameNorm)
            .HasDatabaseName("ix_link_resolved_subdivisions_name_norm");

        b.HasIndex(x => new { x.IsActive, x.NameNorm })
            .HasDatabaseName("ix_link_resolved_subdivisions_active_name_norm");
    }
}
