//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.Infrastructure.Sqlite.Configurations;

internal sealed class DailyReportConfiguration
    : IEntityTypeConfiguration<DailyReport>
{
    public void Configure(EntityTypeBuilder<DailyReport> builder)
    {
        builder.ToTable("daily_reports");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ReportDate)
            .HasColumnName("report_date")
            .IsRequired();

        builder.Property(x => x.TotalMessages)
            .HasColumnName("total_messages")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.GeneratedBy)
            .HasColumnName("generated_by")
            .HasMaxLength(200);

        builder.Property(x => x.GeneratedAt)
            .HasColumnName("generated_at");

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at");

        // Зв'язок з групами повідомлень
        builder.HasMany(x => x.Groups)
            .WithOne(x => x.DailyReport)
            .HasForeignKey(x => x.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Зв'язок з матрицею (один до одного)
        builder.HasOne(x => x.Matrix)
            .WithOne(x => x.DailyReport)
            .HasForeignKey<ParticipantMatrix>(x => x.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Лише один Published або Draft звіт за кожен день
        builder.HasIndex(x => new { x.ReportDate, x.Status })
            .HasDatabaseName("ix_daily_reports_date_status");
    }
}
