//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class InterceptionMessageConfiguration
    : IEntityTypeConfiguration<InterceptionMessage>
{
    public void Configure(EntityTypeBuilder<InterceptionMessage> builder)
    {
        builder.ToTable("interception_messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ObservedDate)
            .HasColumnName("observed_date")
            .IsRequired();

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(50);

        builder.Property(x => x.Division)
            .HasColumnName("division")
            .HasMaxLength(100);

        builder.Property(x => x.PointSignal)
            .HasColumnName("point_signal")
            .HasMaxLength(200);

        builder.Property(x => x.VectorSignal)
            .HasColumnName("vector_signal")
            .HasMaxLength(100);

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // FK → InterceptionAction
        builder.Property(x => x.InterceptionActionId)
            .HasColumnName("interception_action_id");

        builder.HasOne(x => x.InterceptionAction)
            .WithMany()
            .HasForeignKey(x => x.InterceptionActionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Owned collections
        builder.HasMany(x => x.Participants)
            .WithOne(x => x.InterceptionMessage)
            .HasForeignKey(x => x.InterceptionMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Labels)
            .WithOne()
            .HasForeignKey("InterceptionMessageId")
            .OnDelete(DeleteBehavior.Cascade);

        // Індекс для швидкого пошуку по даті
        builder.HasIndex(x => x.ObservedDate)
            .HasDatabaseName("ix_interception_messages_observed_date");

        // Індекс для фільтрації по частоті (основна ознака pattern matching)
        builder.HasIndex(x => x.Frequency)
            .HasDatabaseName("ix_interception_messages_frequency");

        // Композитний індекс для grouping у DailyReportService
        builder.HasIndex(x => new { x.Frequency, x.VectorSignal, x.Division })
            .HasDatabaseName("ix_interception_messages_grouping");
    }
}
