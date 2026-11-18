using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrewStop.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Promotions",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPoints",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    PointsDelta = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_OwnerUserId",
                table: "Promotions",
                column: "OwnerUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_LoyaltyPoints",
                table: "AspNetUsers",
                sql: "`LoyaltyPoints` >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_OrderId_Reason",
                table: "LoyaltyTransactions",
                columns: new[] { "OrderId", "Reason" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_UserId_CreatedAtUtc",
                table: "LoyaltyTransactions",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_OwnerUserId",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_LoyaltyPoints",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "LoyaltyPoints",
                table: "AspNetUsers");
        }
    }
}
