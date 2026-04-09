using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class PersonDirectiveRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canonical_persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_person_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    canonical_person_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    resolved_participant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    added_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_person_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_canonical_person_members_canonical_persons_canonical_person_id",
                        column: x => x.canonical_person_id,
                        principalTable: "canonical_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_person_members_resolved_participants_resolved_participant_id",
                        column: x => x.resolved_participant_id,
                        principalTable: "resolved_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_directive_relations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_canonical_person_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    to_canonical_person_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    relation_type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    confidence = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    source_observation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    is_manual = table.Column<bool>(type: "INTEGER", nullable: false),
                    comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_directive_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_directive_relations_canonical_persons_from_canonical_person_id",
                        column: x => x.from_canonical_person_id,
                        principalTable: "canonical_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_directive_relations_canonical_persons_to_canonical_person_id",
                        column: x => x.to_canonical_person_id,
                        principalTable: "canonical_persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_canonical_person_members_canonical_person_id",
                table: "canonical_person_members",
                column: "canonical_person_id");

            migrationBuilder.CreateIndex(
                name: "ux_canonical_person_members_resolved_participant_id",
                table: "canonical_person_members",
                column: "resolved_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_canonical_persons_display_name",
                table: "canonical_persons",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_from",
                table: "person_directive_relations",
                column: "from_canonical_person_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_to",
                table: "person_directive_relations",
                column: "to_canonical_person_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_directive_relations_unique_pair",
                table: "person_directive_relations",
                columns: new[] { "from_canonical_person_id", "to_canonical_person_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canonical_person_members");

            migrationBuilder.DropTable(
                name: "person_directive_relations");

            migrationBuilder.DropTable(
                name: "canonical_persons");
        }
    }
}
