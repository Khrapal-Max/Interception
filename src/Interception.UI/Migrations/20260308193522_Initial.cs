using Microsoft.EntityFrameworkCore.Migrations;

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
                name: "link_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_date = table.Column<DateOnly>(type: "date", nullable: false),
                    day_part = table.Column<short>(type: "smallint", nullable: false),
                    layer = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    rm_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    point_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    location_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    district_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_row = table.Column<int>(type: "integer", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observations", x => x.Id);
                    table.CheckConstraint("ck_link_observations_day_part", "day_part in (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "link_resolved_actors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    callsign = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_resolved_actors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "link_observation_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_raw = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    label_norm = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_unknown = table.Column<bool>(type: "boolean", nullable: false),
                    role_raw = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observation_participants", x => x.Id);
                    table.CheckConstraint("ck_link_obs_participants_ordinal", "ordinal >= 1");
                    table.ForeignKey(
                        name: "FK_link_observation_participants_link_observations_observation~",
                        column: x => x.observation_id,
                        principalTable: "link_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_clusters", x => x.Id);
                    table.CheckConstraint("ck_link_unknown_clusters_status", "status in ('open', 'resolved', 'merged', 'archived')");
                    table.ForeignKey(
                        name: "FK_link_unknown_clusters_link_resolved_actors_resolved_actor_id",
                        column: x => x.resolved_actor_id,
                        principalTable: "link_resolved_actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_cluster_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    unknown_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    added_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    added_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_cluster_members", x => x.Id);
                    table.CheckConstraint("ck_link_unknown_cluster_members_confidence", "confidence is null or (confidence >= 0 and confidence <= 1)");
                    table.ForeignKey(
                        name: "FK_link_unknown_cluster_members_link_observation_participants_~",
                        column: x => x.observation_participant_id,
                        principalTable: "link_observation_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_link_unknown_cluster_members_link_unknown_clusters_unknown_~",
                        column: x => x.unknown_cluster_id,
                        principalTable: "link_unknown_clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_link_obs_participants_label_norm",
                table: "link_observation_participants",
                column: "label_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_obs_participants_observation_id",
                table: "link_observation_participants",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_obs_participants_observation_label_norm",
                table: "link_observation_participants",
                columns: new[] { "observation_id", "label_norm" },
                unique: true,
                filter: "is_unknown = false and label_norm is not null");

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_action_norm",
                table: "link_observations",
                column: "action_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_date_part",
                table: "link_observations",
                columns: new[] { "observed_date", "day_part" });

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_source_row",
                table: "link_observations",
                columns: new[] { "source_file_id", "source_row" });

            migrationBuilder.CreateIndex(
                name: "ux_link_observations_content_hash",
                table: "link_observations",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_resolved_actors_display_name",
                table: "link_resolved_actors",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_cluster_members_cluster_id",
                table: "link_unknown_cluster_members",
                column: "unknown_cluster_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_unknown_cluster_members_participant_id",
                table: "link_unknown_cluster_members",
                column: "observation_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_clusters_resolved_actor_id",
                table: "link_unknown_clusters",
                column: "resolved_actor_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_unknown_clusters_code",
                table: "link_unknown_clusters",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link_unknown_cluster_members");

            migrationBuilder.DropTable(
                name: "link_observation_participants");

            migrationBuilder.DropTable(
                name: "link_unknown_clusters");

            migrationBuilder.DropTable(
                name: "link_observations");

            migrationBuilder.DropTable(
                name: "link_resolved_actors");
        }
    }
}
