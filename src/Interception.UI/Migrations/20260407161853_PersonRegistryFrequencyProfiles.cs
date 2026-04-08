using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class PersonRegistryFrequencyProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_resolved_participants_name",
                table: "resolved_participants");

            migrationBuilder.AddColumn<string>(
                name: "frequency",
                table: "resolved_participants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_resolved_participants_name_frequency_division",
                table: "resolved_participants",
                columns: new[] { "name", "frequency", "division" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_resolved_participants_name_frequency_division",
                table: "resolved_participants");

            migrationBuilder.DropColumn(
                name: "frequency",
                table: "resolved_participants");

            migrationBuilder.CreateIndex(
                name: "ix_resolved_participants_name",
                table: "resolved_participants",
                column: "name",
                unique: true);
        }
    }
}
