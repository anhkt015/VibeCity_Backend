using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VibeCity_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTeleportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NpcType",
                table: "Npcs",
                newName: "npctype");

            migrationBuilder.AddColumn<int>(
                name: "survived_days",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "total_survival_minutes",
                table: "Students",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "unlocked_skills",
                table: "Students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "vibe_coin",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsServerChung",
                table: "Buildings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentId",
                table: "Buildings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "teleport_tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    ticket_code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teleport_tickets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teleport_tickets");

            migrationBuilder.DropColumn(
                name: "survived_days",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "total_survival_minutes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "unlocked_skills",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "vibe_coin",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsServerChung",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "npctype",
                table: "Npcs",
                newName: "NpcType");

            migrationBuilder.AlterColumn<string>(
                name: "Timestamp",
                table: "Buildings",
                type: "text",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
