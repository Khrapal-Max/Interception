//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

/// <summary>
/// Конфігурація канонічних акторів, у яких може завершуватися резолюція unknown-кластерів.
/// </summary>
public sealed class ResolvedActorConfiguration : IEntityTypeConfiguration<ResolvedActor>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ResolvedActor> b)
    {
        b.ToTable("link_resolved_actors");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(128);

        b.Property(x => x.Callsign)
            .HasColumnName("callsign")
            .HasMaxLength(128);

        b.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(1024);

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128);

        b.HasIndex(x => x.DisplayName)
            .HasDatabaseName("ix_link_resolved_actors_display_name");
    }
}