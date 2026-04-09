//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class PersonDirectiveRelationConfiguration
    : IEntityTypeConfiguration<PersonDirectiveRelation>
{
    public void Configure(EntityTypeBuilder<PersonDirectiveRelation> builder)
    {
        builder.ToTable("person_directive_relations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.FromCanonicalPersonId)
            .HasColumnName("from_canonical_person_id");

        builder.Property(x => x.FromResolvedParticipantId)
            .HasColumnName("from_resolved_participant_id");

        builder.Property(x => x.ToCanonicalPersonId)
            .HasColumnName("to_canonical_person_id");

        builder.Property(x => x.ToResolvedParticipantId)
            .HasColumnName("to_resolved_participant_id");

        builder.Property(x => x.RelationType)
            .HasColumnName("relation_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SourceObservationId)
            .HasColumnName("source_observation_id");

        builder.Property(x => x.IsManual)
            .HasColumnName("is_manual")
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.FromCanonicalPersonId)
            .HasDatabaseName("ix_person_directive_relations_from_canonical");

        builder.HasIndex(x => x.FromResolvedParticipantId)
            .HasDatabaseName("ix_person_directive_relations_from_resolved");

        builder.HasIndex(x => x.ToCanonicalPersonId)
            .HasDatabaseName("ix_person_directive_relations_to_canonical");

        builder.HasIndex(x => x.ToResolvedParticipantId)
            .HasDatabaseName("ix_person_directive_relations_to_resolved");

        builder.HasOne(x => x.FromCanonicalPerson)
            .WithMany()
            .HasForeignKey(x => x.FromCanonicalPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToCanonicalPerson)
            .WithMany()
            .HasForeignKey(x => x.ToCanonicalPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FromResolvedParticipant)
            .WithMany()
            .HasForeignKey(x => x.FromResolvedParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToResolvedParticipant)
            .WithMany()
            .HasForeignKey(x => x.ToResolvedParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
