using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class AddTopologyGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topology_snapshot_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    date_from_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    date_to_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    is_stale = table.Column<bool>(type: "INTEGER", nullable: false),
                    group_count = table.Column<int>(type: "INTEGER", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    run_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_key = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    division = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    key_person_name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    key_person_role = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    mention_count = table.Column<int>(type: "INTEGER", nullable: false),
                    internal_connection_weight = table.Column<int>(type: "INTEGER", nullable: false),
                    bridge_weight = table.Column<int>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_groups_topology_snapshot_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "topology_snapshot_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_bridges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_group_key = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    target_division = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    contact_person_name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    bridge_frequency = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    weight = table.Column<int>(type: "INTEGER", nullable: false),
                    primary_action = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_bridges", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_bridges_topology_snapshot_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "topology_snapshot_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_group_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    is_primary = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_group_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_group_actions_topology_snapshot_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "topology_snapshot_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_group_frequencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    frequency = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_group_frequencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_group_frequencies_topology_snapshot_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "topology_snapshot_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    mention_count = table.Column<int>(type: "INTEGER", nullable: false),
                    unique_partner_count = table.Column<int>(type: "INTEGER", nullable: false),
                    connection_weight = table.Column<int>(type: "INTEGER", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    group_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_shared_across_groups = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_key_person = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_group_members_topology_snapshot_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "topology_snapshot_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topology_snapshot_bridge_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bridge_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    is_primary = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topology_snapshot_bridge_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_topology_snapshot_bridge_actions_topology_snapshot_bridges_bridge_id",
                        column: x => x.bridge_id,
                        principalTable: "topology_snapshot_bridges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_bridge_actions_bridge_id",
                table: "topology_snapshot_bridge_actions",
                column: "bridge_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_bridges_group_id",
                table: "topology_snapshot_bridges",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_bridges_group_target_group",
                table: "topology_snapshot_bridges",
                columns: new[] { "group_id", "target_group_key" });

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_group_actions_group_id",
                table: "topology_snapshot_group_actions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_group_frequencies_group_id",
                table: "topology_snapshot_group_frequencies",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_group_members_group_id",
                table: "topology_snapshot_group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_groups_run_group_key",
                table: "topology_snapshot_groups",
                columns: new[] { "run_id", "group_key" });

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_groups_run_id",
                table: "topology_snapshot_groups",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_runs_completed_at",
                table: "topology_snapshot_runs",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_runs_period_status",
                table: "topology_snapshot_runs",
                columns: new[] { "date_from_utc", "date_to_utc", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_topology_snapshot_runs_status",
                table: "topology_snapshot_runs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topology_snapshot_bridge_actions");

            migrationBuilder.DropTable(
                name: "topology_snapshot_group_actions");

            migrationBuilder.DropTable(
                name: "topology_snapshot_group_frequencies");

            migrationBuilder.DropTable(
                name: "topology_snapshot_group_members");

            migrationBuilder.DropTable(
                name: "topology_snapshot_bridges");

            migrationBuilder.DropTable(
                name: "topology_snapshot_groups");

            migrationBuilder.DropTable(
                name: "topology_snapshot_runs");
        }
    }
}
