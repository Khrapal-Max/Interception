using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "daily_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_messages = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interception_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interception_actions", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "message_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    common_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    common_vector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    common_division = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_message_groups_daily_reports_daily_report_id",
                        column: x => x.daily_report_id,
                        principalTable: "daily_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_matrices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_report_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_matrices", x => x.id);
                    table.ForeignKey(
                        name: "FK_participant_matrices_daily_reports_daily_report_id",
                        column: x => x.daily_report_id,
                        principalTable: "daily_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interception_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    division = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    point_signal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    vector_signal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    interception_action_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interception_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_interception_messages_interception_actions_interception_act~",
                        column: x => x.interception_action_id,
                        principalTable: "interception_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participant_candidate_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", precision: 4, scale: 3, nullable: false),
                    suggested_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggested_role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggested_division = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    reason_same_frequency = table.Column<bool>(type: "boolean", nullable: false),
                    reason_same_vector = table.Column<bool>(type: "boolean", nullable: false),
                    reason_same_point_signal = table.Column<bool>(type: "boolean", nullable: false),
                    reason_same_division = table.Column<bool>(type: "boolean", nullable: false),
                    reason_close_in_time = table.Column<bool>(type: "boolean", nullable: false),
                    reason_shared_partners = table.Column<bool>(type: "boolean", nullable: false),
                    reason_shared_labels = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolved_participant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_candidate_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_participant_candidate_groups_resolved_participants_resolved~",
                        column: x => x.resolved_participant_id,
                        principalTable: "resolved_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "message_group_entries",
                columns: table => new
                {
                    message_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_group_entries", x => new { x.message_group_id, x.message_id });
                    table.ForeignKey(
                        name: "FK_message_group_entries_message_groups_message_group_id",
                        column: x => x.message_group_id,
                        principalTable: "message_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matrix_cells",
                columns: table => new
                {
                    matrix_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_a = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    participant_b = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    interaction_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrix_cells", x => new { x.matrix_id, x.participant_a, x.participant_b });
                    table.ForeignKey(
                        name: "FK_matrix_cells_participant_matrices_matrix_id",
                        column: x => x.matrix_id,
                        principalTable: "participant_matrices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interception_message_labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    interception_message_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interception_message_labels", x => x.id);
                    table.ForeignKey(
                        name: "FK_interception_message_labels_interception_messages_intercept~",
                        column: x => x.interception_message_id,
                        principalTable: "interception_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interception_message_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interception_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_unknown = table.Column<bool>(type: "boolean", nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interception_message_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_interception_message_participants_interception_messages_int~",
                        column: x => x.interception_message_id,
                        principalTable: "interception_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_candidate_group_refs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    candidate_group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_candidate_group_refs", x => x.id);
                    table.ForeignKey(
                        name: "FK_participant_candidate_group_refs_participant_candidate_grou~",
                        column: x => x.candidate_group_id,
                        principalTable: "participant_candidate_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daily_reports_date_status",
                table: "daily_reports",
                columns: new[] { "report_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_interception_actions_name",
                table: "interception_actions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labels_message_name",
                table: "interception_message_labels",
                columns: new[] { "interception_message_id", "name_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participants_message_ordinal",
                table: "interception_message_participants",
                columns: new[] { "interception_message_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participants_name",
                table: "interception_message_participants",
                column: "name",
                filter: "name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_interception_messages_frequency",
                table: "interception_messages",
                column: "frequency");

            migrationBuilder.CreateIndex(
                name: "ix_interception_messages_grouping",
                table: "interception_messages",
                columns: new[] { "frequency", "vector_signal", "division" });

            migrationBuilder.CreateIndex(
                name: "IX_interception_messages_interception_action_id",
                table: "interception_messages",
                column: "interception_action_id");

            migrationBuilder.CreateIndex(
                name: "ix_interception_messages_observed_date",
                table: "interception_messages",
                column: "observed_date");

            migrationBuilder.CreateIndex(
                name: "ix_matrix_cells_count",
                table: "matrix_cells",
                columns: new[] { "matrix_id", "interaction_count" });

            migrationBuilder.CreateIndex(
                name: "ix_message_groups_key",
                table: "message_groups",
                column: "group_key");

            migrationBuilder.CreateIndex(
                name: "ix_message_groups_report_id",
                table: "message_groups",
                column: "daily_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_participant_candidate_group_refs_candidate_group_id_partici~",
                table: "participant_candidate_group_refs",
                columns: new[] { "candidate_group_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_groups_confidence",
                table: "participant_candidate_groups",
                column: "confidence_score");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_groups_resolved_participant",
                table: "participant_candidate_groups",
                column: "resolved_participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_groups_status",
                table: "participant_candidate_groups",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_participant_matrices_daily_report_id",
                table: "participant_matrices",
                column: "daily_report_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resolved_participants_name",
                table: "resolved_participants",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interception_message_labels");

            migrationBuilder.DropTable(
                name: "interception_message_participants");

            migrationBuilder.DropTable(
                name: "matrix_cells");

            migrationBuilder.DropTable(
                name: "message_group_entries");

            migrationBuilder.DropTable(
                name: "participant_candidate_group_refs");

            migrationBuilder.DropTable(
                name: "interception_messages");

            migrationBuilder.DropTable(
                name: "participant_matrices");

            migrationBuilder.DropTable(
                name: "message_groups");

            migrationBuilder.DropTable(
                name: "participant_candidate_groups");

            migrationBuilder.DropTable(
                name: "interception_actions");

            migrationBuilder.DropTable(
                name: "daily_reports");

            migrationBuilder.DropTable(
                name: "resolved_participants");
        }
    }
}
