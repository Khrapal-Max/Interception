//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.UI.Infrastructure.Configurations;

internal sealed class InterceptionMessageParticipantConfiguration
    : IEntityTypeConfiguration<InterceptionMessageParticipant>
{
    public void Configure(EntityTypeBuilder<InterceptionMessageParticipant> builder)
    {
        builder.ToTable("interception_message_participants");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.InterceptionMessageId)
            .HasColumnName("interception_message_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200);

        builder.Property(x => x.IsUnknown)
            .HasColumnName("is_unknown")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(100);

        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal")
            .IsRequired();

        // Унікальність: один порядковий номер в межах одного повідомлення
        builder.HasIndex(x => new { x.InterceptionMessageId, x.Ordinal })
            .IsUnique()
            .HasDatabaseName("ix_participants_message_ordinal");

        // Індекс для pattern matching — пошук по імені відомих учасників
        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_participants_name")
            .HasFilter("name IS NOT NULL");
    }
}
