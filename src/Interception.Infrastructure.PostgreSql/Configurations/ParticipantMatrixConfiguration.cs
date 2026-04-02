//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.Infrastructure.PostgreSql.Configurations;

internal sealed class ParticipantMatrixConfiguration
    : IEntityTypeConfiguration<ParticipantMatrix>
{
    public void Configure(EntityTypeBuilder<ParticipantMatrix> builder)
    {
        builder.ToTable("participant_matrices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.DailyReportId)
            .HasColumnName("daily_report_id")
            .IsRequired();

        // MatrixCell зберігаємо як owned collection → окрема таблиця
        builder.OwnsMany(x => x.Cells, c =>
        {
            c.ToTable("matrix_cells");

            c.WithOwner().HasForeignKey(x => x.MatrixId);

            c.Property(x => x.MatrixId)
                .HasColumnName("matrix_id")
                .IsRequired();

            c.Property(x => x.ParticipantA)
                .HasColumnName("participant_a")
                .HasMaxLength(200)
                .IsRequired();

            c.Property(x => x.ParticipantB)
                .HasColumnName("participant_b")
                .HasMaxLength(200)
                .IsRequired();

            c.Property(x => x.InteractionCount)
                .HasColumnName("interaction_count")
                .IsRequired();

            // ParticipantA < ParticipantB гарантується доменним кодом — немає дублів
            c.HasKey(x => new { x.MatrixId, x.ParticipantA, x.ParticipantB });

            // Індекс для топ-N запитів (сортування по interaction_count)
            c.HasIndex(x => new { x.MatrixId, x.InteractionCount })
                .HasDatabaseName("ix_matrix_cells_count");
        });
    }
}
