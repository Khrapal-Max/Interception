//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Interception.Infrastructure.Configurations;

internal sealed class InterceptionMessageLabelConfiguration
    : IEntityTypeConfiguration<InterceptionMessageLabel>
{
    public void Configure(EntityTypeBuilder<InterceptionMessageLabel> builder)
    {
        builder.ToTable("interception_message_labels");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Shadow property — FK до InterceptionMessage (налаштовується через HasMany/WithOne)
        builder.Property<Guid>("InterceptionMessageId")
            .HasColumnName("interception_message_id")
            .IsRequired();

        builder.Property(x => x.NameLabel)
            .HasColumnName("name_label")
            .HasMaxLength(1000)
            .IsRequired();

        // Унікальність мітки в межах одного повідомлення
        builder.HasIndex("InterceptionMessageId", nameof(InterceptionMessageLabel.NameLabel))
            .IsUnique()
            .HasDatabaseName("ix_labels_message_name");
    }
}
