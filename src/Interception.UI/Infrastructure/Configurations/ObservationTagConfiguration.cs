//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ObservationTagConfiguration : IEntityTypeConfiguration<ObservationTag>
{
    public void Configure(EntityTypeBuilder<ObservationTag> b)
    {
        b.ToTable("link_observation_tags");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.ObservationId)
            .HasColumnName("observation_id")
            .IsRequired();

        b.Property(x => x.TagCatalogId)
            .HasColumnName("tag_catalog_id");

        b.Property(x => x.RawValue)
            .HasColumnName("raw_value")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.RawValueNorm)
            .HasColumnName("raw_value_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.HasOne(x => x.Observation)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.ObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.TagCatalog)
            .WithMany(x => x.ObservationTags)
            .HasForeignKey(x => x.TagCatalogId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ObservationId)
            .HasDatabaseName("ix_link_observation_tags_observation_id");

        b.HasIndex(x => x.TagCatalogId)
            .HasDatabaseName("ix_link_observation_tags_tag_catalog_id");

        b.HasIndex(x => new { x.ObservationId, x.Kind, x.RawValueNorm })
            .IsUnique()
            .HasDatabaseName("ux_link_observation_tags_observation_kind_raw_value_norm");

        b.HasIndex(x => new { x.Kind, x.RawValueNorm })
            .HasDatabaseName("ix_link_observation_tags_kind_raw_value_norm");
    }
}
