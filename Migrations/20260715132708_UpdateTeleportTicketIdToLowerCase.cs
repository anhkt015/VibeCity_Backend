using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeCity_API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeleportTicketIdToLowerCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "teleport_tickets",
                newName: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "id",
                table: "teleport_tickets",
                newName: "Id");
        }
    }
}
