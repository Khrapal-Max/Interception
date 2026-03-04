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
                    layer = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    rm_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    point_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    location_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    district_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    company_raw = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                filter: "label_norm is not null");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link_observation_participants");

            migrationBuilder.DropTable(
                name: "link_observations");
        }
    }
}
