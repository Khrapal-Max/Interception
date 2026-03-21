//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class ParticipantCandidateGroupConfiguration
    : IEntityTypeConfiguration<ParticipantCandidateGroup>
{
    public void Configure(EntityTypeBuilder<ParticipantCandidateGroup> builder)
    {
        builder.ToTable("participant_candidate_groups");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasPrecision(4, 3)
            .IsRequired();

        builder.Property(x => x.SuggestedName)
            .HasColumnName("suggested_name")
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ResolvedBy)
            .HasColumnName("resolved_by")
            .HasMaxLength(200);

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // PatternMatchReasons зберігаємо як owned entity (один рядок у тій самій таблиці)
        builder.OwnsOne(x => x.Reasons, r =>
        {
            r.Property(x => x.SameFrequency)
                .HasColumnName("reason_same_frequency")
                .IsRequired();

            r.Property(x => x.SameVector)
                .HasColumnName("reason_same_vector")
                .IsRequired();

            r.Property(x => x.SamePointSignal)
                .HasColumnName("reason_same_point_signal")
                .IsRequired();

            r.Property(x => x.SameDivision)
                .HasColumnName("reason_same_division")
                .IsRequired();

            r.Property(x => x.CloseInTime)
                .HasColumnName("reason_close_in_time")
                .IsRequired();
        });

        // ParticipantRefs зберігаємо як owned collection → окрема таблиця
        builder.OwnsMany(x => x.ParticipantRefs, pr =>
        {
            pr.ToTable("participant_candidate_group_refs");

            pr.WithOwner().HasForeignKey("CandidateGroupId");
            pr.Property<Guid>("CandidateGroupId").HasColumnName("candidate_group_id");

            pr.Property(x => x.MessageId)
                .HasColumnName("message_id")
                .IsRequired();

            pr.Property(x => x.ParticipantId)
                .HasColumnName("participant_id")
                .IsRequired();

            pr.Property(x => x.Ordinal)
                .HasColumnName("ordinal")
                .IsRequired();

            pr.HasKey("CandidateGroupId", nameof(ParticipantRef.ParticipantId));
        });

        // Індекс для фільтрації Open-груп (найчастіший запит в UI)
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_candidate_groups_status");

        builder.HasIndex(x => x.ConfidenceScore)
            .HasDatabaseName("ix_candidate_groups_confidence");
    }
}
