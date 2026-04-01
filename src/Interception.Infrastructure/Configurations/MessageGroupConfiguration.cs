//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.Infrastructure.Configurations;

internal sealed class MessageGroupConfiguration
    : IEntityTypeConfiguration<MessageGroup>
{
    public void Configure(EntityTypeBuilder<MessageGroup> builder)
    {
        builder.ToTable("message_groups");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.DailyReportId)
            .HasColumnName("daily_report_id")
            .IsRequired();

        builder.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasMaxLength(200);

        builder.Property(x => x.CommonFrequency)
            .HasColumnName("common_frequency")
            .HasMaxLength(50);

        builder.Property(x => x.CommonVector)
            .HasColumnName("common_vector")
            .HasMaxLength(100);

        builder.Property(x => x.CommonDivision)
            .HasColumnName("common_division")
            .HasMaxLength(100);

        // MessageGroupEntry зберігаємо як owned collection → окрема таблиця
        builder.OwnsMany(x => x.Entries, e =>
        {
            e.ToTable("message_group_entries");

            e.WithOwner().HasForeignKey(x => x.MessageGroupId);

            e.Property(x => x.MessageGroupId)
                .HasColumnName("message_group_id")
                .IsRequired();

            e.Property(x => x.MessageId)
                .HasColumnName("message_id")
                .IsRequired();

            e.HasKey(x => new { x.MessageGroupId, x.MessageId });
        });

        builder.HasIndex(x => x.DailyReportId)
            .HasDatabaseName("ix_message_groups_report_id");

        builder.HasIndex(x => x.GroupKey)
            .HasDatabaseName("ix_message_groups_key");
    }
}
