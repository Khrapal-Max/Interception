//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class CanonicalPersonMemberConfiguration : IEntityTypeConfiguration<CanonicalPersonMember>
{
    public void Configure(EntityTypeBuilder<CanonicalPersonMember> builder)
    {
        builder.ToTable("canonical_person_members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.CanonicalPersonId)
            .HasColumnName("canonical_person_id")
            .IsRequired();

        builder.Property(x => x.ResolvedParticipantId)
            .HasColumnName("resolved_participant_id")
            .IsRequired();

        builder.Property(x => x.AddedAtUtc)
            .HasColumnName("added_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.CanonicalPersonId)
            .HasDatabaseName("ix_canonical_person_members_canonical_person_id");

        builder.HasIndex(x => x.ResolvedParticipantId)
            .IsUnique()
            .HasDatabaseName("ux_canonical_person_members_resolved_participant_id");

        builder.HasOne(x => x.CanonicalPerson)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.CanonicalPersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResolvedParticipant)
            .WithMany()
            .HasForeignKey(x => x.ResolvedParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
