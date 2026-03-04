//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

public sealed class ObservationConfiguration : IEntityTypeConfiguration<Observation>
{
    public void Configure(EntityTypeBuilder<Observation> b)
    {
        b.ToTable("link_observations", tb =>
        {
            tb.HasCheckConstraint("ck_link_observations_day_part", "day_part in (1, 2)");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.ObservedDate)
            .HasColumnName("observed_date")
            .HasColumnType("date")
            .IsRequired();

        b.Property(x => x.DayPart)
            .HasColumnName("day_part")
            .HasConversion<short>()           // enum -> smallint
            .HasColumnType("smallint")
            .IsRequired();

        b.Property(x => x.Layer)
            .HasColumnName("layer")
            .HasPrecision(10, 4);

        b.Property(x => x.RmRaw)
            .HasColumnName("rm_raw")
            .HasMaxLength(256);

        b.Property(x => x.PointRaw)
            .HasColumnName("point_raw")
            .HasMaxLength(256);

        b.Property(x => x.LocationRaw)
            .HasColumnName("location_raw")
            .HasMaxLength(256);

        b.Property(x => x.DistrictRaw)
            .HasColumnName("district_raw")
            .HasMaxLength(256);

        b.Property(x => x.CompanyRaw)
            .HasColumnName("company_raw")
            .HasMaxLength(256);

        b.Property(x => x.ActionRaw)
            .HasColumnName("action_raw")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.ActionNorm)
            .HasColumnName("action_norm")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(x => x.SourceFileId)
            .HasColumnName("source_file_id");

        b.Property(x => x.SourceRow)
            .HasColumnName("source_row");

        b.Property(x => x.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(64) // SHA-256 hex = 64
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        // Relationships
        b.HasMany(x => x.Participants)
            .WithOne(x => x.Observation)
            .HasForeignKey(x => x.ObservationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes (реєстр/пошук/імпорт)
        b.HasIndex(x => new { x.ObservedDate, x.DayPart })
            .HasDatabaseName("ix_link_observations_date_part");

        b.HasIndex(x => x.ActionNorm)
            .HasDatabaseName("ix_link_observations_action_norm");

        // Ідемпотентність імпорту (можна робити unique)
        b.HasIndex(x => x.ContentHash)
            .IsUnique()
            .HasDatabaseName("ux_link_observations_content_hash");

        // Optional: якщо хочеш швидко знаходити рядки конкретного імпорту
        b.HasIndex(x => new { x.SourceFileId, x.SourceRow })
            .HasDatabaseName("ix_link_observations_source_row");
    }
}