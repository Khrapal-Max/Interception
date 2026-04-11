using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class UpProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_person_directive_relations_unique_pair",
                table: "person_directive_relations");

            migrationBuilder.RenameIndex(
                name: "ix_person_directive_relations_to",
                table: "person_directive_relations",
                newName: "ix_person_directive_relations_to_canonical");

            migrationBuilder.RenameIndex(
                name: "ix_person_directive_relations_from",
                table: "person_directive_relations",
                newName: "ix_person_directive_relations_from_canonical");

            migrationBuilder.AlterColumn<Guid>(
                name: "to_canonical_person_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "from_canonical_person_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "from_resolved_participant_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "to_resolved_participant_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "participant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_from_resolved",
                table: "person_directive_relations",
                column: "from_resolved_participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_to_resolved",
                table: "person_directive_relations",
                column: "to_resolved_participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_participant_roles_name",
                table: "participant_roles",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_person_directive_relations_resolved_participants_from_resolved_participant_id",
                table: "person_directive_relations",
                column: "from_resolved_participant_id",
                principalTable: "resolved_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_person_directive_relations_resolved_participants_to_resolved_participant_id",
                table: "person_directive_relations",
                column: "to_resolved_participant_id",
                principalTable: "resolved_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_person_directive_relations_resolved_participants_from_resolved_participant_id",
                table: "person_directive_relations");

            migrationBuilder.DropForeignKey(
                name: "FK_person_directive_relations_resolved_participants_to_resolved_participant_id",
                table: "person_directive_relations");

            migrationBuilder.DropTable(
                name: "participant_roles");

            migrationBuilder.DropIndex(
                name: "ix_person_directive_relations_from_resolved",
                table: "person_directive_relations");

            migrationBuilder.DropIndex(
                name: "ix_person_directive_relations_to_resolved",
                table: "person_directive_relations");

            migrationBuilder.DropColumn(
                name: "from_resolved_participant_id",
                table: "person_directive_relations");

            migrationBuilder.DropColumn(
                name: "to_resolved_participant_id",
                table: "person_directive_relations");

            migrationBuilder.RenameIndex(
                name: "ix_person_directive_relations_to_canonical",
                table: "person_directive_relations",
                newName: "ix_person_directive_relations_to");

            migrationBuilder.RenameIndex(
                name: "ix_person_directive_relations_from_canonical",
                table: "person_directive_relations",
                newName: "ix_person_directive_relations_from");

            migrationBuilder.AlterColumn<Guid>(
                name: "to_canonical_person_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "from_canonical_person_id",
                table: "person_directive_relations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_unique_pair",
                table: "person_directive_relations",
                columns: new[] { "from_canonical_person_id", "to_canonical_person_id" },
                unique: true);
        }
    }
}
