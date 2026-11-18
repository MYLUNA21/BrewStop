using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrewStop.Migrations
{
    /// <inheritdoc />
    public partial class AddBaristaShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaristaShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BaristaUserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScheduledStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ScheduledEnd = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClockInAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClockOutAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ShiftStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaristaShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaristaShifts_AspNetUsers_BaristaUserId",
                        column: x => x.BaristaUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BaristaShifts_BaristaUserId_ScheduledStart",
                table: "BaristaShifts",
                columns: new[] { "BaristaUserId", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "IX_BaristaShifts_ClockInAt",
                table: "BaristaShifts",
                column: "ClockInAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaristaShifts");
        }
    }
}
