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
                name: "link_observation_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<short>(type: "smallint", nullable: false),
                    initiator_role_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    responder_role_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    typical_participants_count = table.Column<short>(type: "smallint", nullable: true),
                    requires_counterparty = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observation_actions", x => x.Id);
                    table.CheckConstraint("ck_link_observation_actions_typical_participants_count", "typical_participants_count is null or typical_participants_count >= 1");
                });

            migrationBuilder.CreateTable(
                name: "link_resolved_actors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    primary_role = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_resolved_actors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "link_resolved_subdivisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    layer_hint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    rm_hint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_resolved_subdivisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "link_tag_catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_tag_catalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "link_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    observation_action_id = table.Column<Guid>(type: "uuid", nullable: true),
                    layer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    rm_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    point_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    location_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    district_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subdivision_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    subdivision_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    subdivision_strength = table.Column<short>(type: "smallint", nullable: true),
                    subdivision_source = table.Column<short>(type: "smallint", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_link_observations_link_observation_actions_observation_acti~",
                        column: x => x.observation_action_id,
                        principalTable: "link_observation_actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    title_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    resolved_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_link_unknown_clusters_link_resolved_actors_resolved_actor_id",
                        column: x => x.resolved_actor_id,
                        principalTable: "link_resolved_actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_subdivision_clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    label_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    layer_hint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    rm_hint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    resolved_subdivision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_subdivision_clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_link_unknown_subdivision_clusters_link_resolved_subdivision~",
                        column: x => x.resolved_subdivision_id,
                        principalTable: "link_resolved_subdivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_observation_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    label_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_unknown = table.Column<bool>(type: "boolean", nullable: false),
                    started_as_unknown = table.Column<bool>(type: "boolean", nullable: false),
                    role_raw = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observation_participants", x => x.Id);
                    table.CheckConstraint("ck_link_observation_participants_ordinal", "ordinal >= 1");
                    table.ForeignKey(
                        name: "FK_link_observation_participants_link_observations_observation~",
                        column: x => x.observation_id,
                        principalTable: "link_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "link_observation_probable_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_action_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observation_probable_actions", x => x.Id);
                    table.CheckConstraint("ck_link_observation_probable_actions_confidence", "confidence >= 0 and confidence <= 1");
                    table.ForeignKey(
                        name: "FK_link_observation_probable_actions_link_observation_actions_~",
                        column: x => x.observation_action_id,
                        principalTable: "link_observation_actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_link_observation_probable_actions_link_observations_observa~",
                        column: x => x.observation_id,
                        principalTable: "link_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "link_observation_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_catalog_id = table.Column<Guid>(type: "uuid", nullable: true),
                    raw_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    raw_value_norm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_observation_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_link_observation_tags_link_observations_observation_id",
                        column: x => x.observation_id,
                        principalTable: "link_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_link_observation_tags_link_tag_catalog_tag_catalog_id",
                        column: x => x.tag_catalog_id,
                        principalTable: "link_tag_catalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_subdivision_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    unknown_subdivision_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_subdivision_observations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_link_unknown_subdivision_observations_link_observations_obs~",
                        column: x => x.observation_id,
                        principalTable: "link_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_link_unknown_subdivision_observations_link_unknown_subdivis~",
                        column: x => x.unknown_subdivision_cluster_id,
                        principalTable: "link_unknown_subdivision_clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "link_unknown_cluster_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    unknown_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_unknown_cluster_members", x => x.Id);
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
                name: "ix_link_observation_actions_active_name_norm",
                table: "link_observation_actions",
                columns: new[] { "is_active", "name_norm" });

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_actions_category",
                table: "link_observation_actions",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ux_link_observation_actions_name_norm",
                table: "link_observation_actions",
                column: "name_norm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_participants_label_norm",
                table: "link_observation_participants",
                column: "label_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_participants_observation_id",
                table: "link_observation_participants",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_observation_participants_observation_label_norm",
                table: "link_observation_participants",
                columns: new[] { "observation_id", "label_norm" },
                unique: true,
                filter: "is_unknown = false and label_norm is not null");

            migrationBuilder.CreateIndex(
                name: "ux_link_observation_participants_observation_ordinal",
                table: "link_observation_participants",
                columns: new[] { "observation_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_probable_actions_observation_action_id",
                table: "link_observation_probable_actions",
                column: "observation_action_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_probable_actions_observation_id",
                table: "link_observation_probable_actions",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_observation_probable_actions_observation_action",
                table: "link_observation_probable_actions",
                columns: new[] { "observation_id", "observation_action_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_tags_kind_raw_value_norm",
                table: "link_observation_tags",
                columns: new[] { "kind", "raw_value_norm" });

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_tags_observation_id",
                table: "link_observation_tags",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_observation_tags_tag_catalog_id",
                table: "link_observation_tags",
                column: "tag_catalog_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_observation_tags_observation_kind_raw_value_norm",
                table: "link_observation_tags",
                columns: new[] { "observation_id", "kind", "raw_value_norm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_action_norm",
                table: "link_observations",
                column: "action_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_layer_rm_raw",
                table: "link_observations",
                columns: new[] { "layer", "rm_raw" });

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_observation_action_id",
                table: "link_observations",
                column: "observation_action_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_observed_date",
                table: "link_observations",
                column: "observed_date");

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_source_row",
                table: "link_observations",
                columns: new[] { "source_file_id", "source_row" });

            migrationBuilder.CreateIndex(
                name: "ix_link_observations_subdivision_norm",
                table: "link_observations",
                column: "subdivision_norm");

            migrationBuilder.CreateIndex(
                name: "ux_link_observations_content_hash",
                table: "link_observations",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_resolved_actors_active_display_name_norm",
                table: "link_resolved_actors",
                columns: new[] { "is_active", "display_name_norm" });

            migrationBuilder.CreateIndex(
                name: "ix_link_resolved_actors_display_name_norm",
                table: "link_resolved_actors",
                column: "display_name_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_resolved_subdivisions_active_name_norm",
                table: "link_resolved_subdivisions",
                columns: new[] { "is_active", "name_norm" });

            migrationBuilder.CreateIndex(
                name: "ix_link_resolved_subdivisions_name_norm",
                table: "link_resolved_subdivisions",
                column: "name_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_tag_catalog_active_kind_name_norm",
                table: "link_tag_catalog",
                columns: new[] { "is_active", "kind", "name_norm" });

            migrationBuilder.CreateIndex(
                name: "ux_link_tag_catalog_kind_name_norm",
                table: "link_tag_catalog",
                columns: new[] { "kind", "name_norm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_cluster_members_unknown_cluster_id",
                table: "link_unknown_cluster_members",
                column: "unknown_cluster_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_unknown_cluster_members_cluster_participant",
                table: "link_unknown_cluster_members",
                columns: new[] { "unknown_cluster_id", "observation_participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_link_unknown_cluster_members_observation_participant_id",
                table: "link_unknown_cluster_members",
                column: "observation_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_clusters_archived_at_utc",
                table: "link_unknown_clusters",
                column: "archived_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_clusters_resolved_actor_id",
                table: "link_unknown_clusters",
                column: "resolved_actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_clusters_title_norm",
                table: "link_unknown_clusters",
                column: "title_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_subdivision_clusters_archived_at_utc",
                table: "link_unknown_subdivision_clusters",
                column: "archived_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_subdivision_clusters_label_norm",
                table: "link_unknown_subdivision_clusters",
                column: "label_norm");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_subdivision_clusters_resolved_subdivision_id",
                table: "link_unknown_subdivision_clusters",
                column: "resolved_subdivision_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_subdivision_observations_cluster_id",
                table: "link_unknown_subdivision_observations",
                column: "unknown_subdivision_cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_link_unknown_subdivision_observations_observation_id",
                table: "link_unknown_subdivision_observations",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "ux_link_unknown_subdivision_observations_cluster_observation",
                table: "link_unknown_subdivision_observations",
                columns: new[] { "unknown_subdivision_cluster_id", "observation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link_observation_probable_actions");

            migrationBuilder.DropTable(
                name: "link_observation_tags");

            migrationBuilder.DropTable(
                name: "link_unknown_cluster_members");

            migrationBuilder.DropTable(
                name: "link_unknown_subdivision_observations");

            migrationBuilder.DropTable(
                name: "link_tag_catalog");

            migrationBuilder.DropTable(
                name: "link_observation_participants");

            migrationBuilder.DropTable(
                name: "link_unknown_clusters");

            migrationBuilder.DropTable(
                name: "link_unknown_subdivision_clusters");

            migrationBuilder.DropTable(
                name: "link_observations");

            migrationBuilder.DropTable(
                name: "link_resolved_actors");

            migrationBuilder.DropTable(
                name: "link_resolved_subdivisions");

            migrationBuilder.DropTable(
                name: "link_observation_actions");
        }
    }
}
