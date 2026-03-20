//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class TagCatalogConfiguration : IEntityTypeConfiguration<TagCatalog>
{
    public void Configure(EntityTypeBuilder<TagCatalog> b)
    {
        b.ToTable("link_tag_catalog");

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

        b.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<short>()
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

        b.HasIndex(x => new { x.Kind, x.NameNorm })
            .IsUnique()
            .HasDatabaseName("ux_link_tag_catalog_kind_name_norm");

        b.HasIndex(x => new { x.IsActive, x.Kind, x.NameNorm })
            .HasDatabaseName("ix_link_tag_catalog_active_kind_name_norm");
    }
}
