using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Interception.UI.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationDetailsToInterceptionMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location_details",
                table: "interception_messages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_details",
                table: "interception_messages");
        }
    }
}
