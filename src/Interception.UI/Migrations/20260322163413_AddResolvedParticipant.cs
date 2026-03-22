using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class AddResolvedParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reason_shared_labels",
                table: "participant_candidate_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reason_shared_partners",
                table: "participant_candidate_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "resolved_participant_id",
                table: "participant_candidate_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_division",
                table: "participant_candidate_groups",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_role",
                table: "participant_candidate_groups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "resolved_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    division = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolved_participants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_groups_resolved_participant",
                table: "participant_candidate_groups",
                column: "resolved_participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_resolved_participants_name",
                table: "resolved_participants",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_candidate_groups_resolved_participants_resolved~",
                table: "participant_candidate_groups",
                column: "resolved_participant_id",
                principalTable: "resolved_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_participant_candidate_groups_resolved_participants_resolved~",
                table: "participant_candidate_groups");

            migrationBuilder.DropTable(
                name: "resolved_participants");

            migrationBuilder.DropIndex(
                name: "ix_candidate_groups_resolved_participant",
                table: "participant_candidate_groups");

            migrationBuilder.DropColumn(
                name: "reason_shared_labels",
                table: "participant_candidate_groups");

            migrationBuilder.DropColumn(
                name: "reason_shared_partners",
                table: "participant_candidate_groups");

            migrationBuilder.DropColumn(
                name: "resolved_participant_id",
                table: "participant_candidate_groups");

            migrationBuilder.DropColumn(
                name: "suggested_division",
                table: "participant_candidate_groups");

            migrationBuilder.DropColumn(
                name: "suggested_role",
                table: "participant_candidate_groups");
        }
    }
}
