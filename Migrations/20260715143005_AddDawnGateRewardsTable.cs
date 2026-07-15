using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VibeCity_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDawnGateRewardsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dawn_gate_rewards",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    night_cycle_id = table.Column<string>(type: "text", nullable: false),
                    reward_amount = table.Column<int>(type: "integer", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dawn_gate_rewards", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dawn_gate_rewards_student_id_night_cycle_id",
                table: "dawn_gate_rewards",
                columns: new[] { "student_id", "night_cycle_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dawn_gate_rewards");
        }
    }
}
