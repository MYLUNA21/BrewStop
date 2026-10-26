using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BrewStop.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Customizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceAdjustment = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customizations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAvailable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsFeatured = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PrepTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrderNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EstimatedReadyAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReadyAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "MenuItemCustomizations",
                columns: table => new
                {
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    CustomizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemCustomizations", x => new { x.MenuItemId, x.CustomizationId });
                    table.ForeignKey(
                        name: "FK_MenuItemCustomizations_Customizations_CustomizationId",
                        column: x => x.CustomizationId,
                        principalTable: "Customizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemCustomizations_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SpecialInstructions = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "OrderItemCustomizations",
                columns: table => new
                {
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    CustomizationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceAdjustment = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemCustomizations", x => new { x.OrderItemId, x.CustomizationId });
                    table.ForeignKey(
                        name: "FK_OrderItemCustomizations_Customizations_CustomizationId",
                        column: x => x.CustomizationId,
                        principalTable: "Customizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItemCustomizations_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.InsertData(
                table: "Customizations",
                columns: new[] { "Id", "DisplayOrder", "IsAvailable", "IsDefault", "Name", "PriceAdjustment", "Type" },
                values: new object[,]
                {
                    { 1, 1, true, true, "Regular", 0m, "Size" },
                    { 2, 2, true, false, "Large", 30m, "Size" },
                    { 3, 1, true, true, "Hot", 0m, "Temperature" },
                    { 4, 2, true, false, "Iced", 0m, "Temperature" },
                    { 5, 1, true, true, "Whole Milk", 0m, "Milk" },
                    { 6, 2, true, false, "Oat Milk", 40m, "Milk" },
                    { 7, 3, true, false, "Almond Milk", 40m, "Milk" },
                    { 8, 1, true, false, "No Sugar", 0m, "Sweetness" },
                    { 9, 2, true, false, "Half Sweet", 0m, "Sweetness" },
                    { 10, 3, true, true, "Full Sweet", 0m, "Sweetness" },
                    { 11, 1, true, false, "Extra Espresso Shot", 30m, "AddOn" },
                    { 12, 2, true, false, "Vanilla Syrup", 20m, "AddOn" },
                    { 13, 3, true, false, "Caramel Syrup", 20m, "AddOn" },
                    { 14, 4, true, false, "Hazelnut Syrup", 20m, "AddOn" },
                    { 15, 5, true, false, "Whipped Cream", 20m, "AddOn" }
                });

            migrationBuilder.InsertData(
                table: "MenuItems",
                columns: new[] { "Id", "BasePrice", "Category", "CreatedAt", "Description", "DisplayOrder", "ImageUrl", "IsAvailable", "IsFeatured", "Name", "PrepTimeMinutes", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 80m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Double shot, rich crema.", 1, "/images/menu/01.webp", true, false, "Espresso", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 100m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Espresso over water. Clean and bold.", 2, "/images/menu/02.webp", true, false, "Americano", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 130m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Espresso with steamed milk.", 3, "/images/menu/03.webp", true, false, "Caffè Latte", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 130m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Equal parts espresso, milk, and foam.", 4, "/images/menu/04.webp", true, false, "Cappuccino", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, 150m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sweetened condensed milk meets espresso.", 5, "/images/menu/05.webp", true, true, "Spanish Latte", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, 160m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vanilla, steamed milk, espresso, caramel drizzle.", 6, "/images/menu/06.webp", true, false, "Caramel Macchiato", 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, 140m, "Coffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "18-hour cold brew topped with sea salt foam.", 7, "/images/menu/07.webp", true, true, "Sea Salt Cold Brew", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, 150m, "NonCoffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ceremonial-grade matcha with milk.", 8, "/images/menu/08.webp", true, true, "Matcha Latte", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, 120m, "NonCoffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fresh strawberry purée and cold milk.", 9, "/images/menu/09.webp", true, false, "Strawberry Milk", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, 120m, "NonCoffee", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rich cocoa, steamed to order.", 10, "/images/menu/10.webp", true, false, "Dark Chocolate", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, 90m, "Tea", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Fresh calamansi and local honey.", 11, "/images/menu/11.webp", true, false, "Calamansi Honey Tea", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, 85m, "Tea", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Black tea shaken with lemon.", 12, "/images/menu/12.webp", true, false, "Iced Lemon Tea", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, 85m, "Pastry", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Flaky, baked fresh each morning.", 13, "/images/menu/13.webp", true, false, "Butter Croissant", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, 60m, "Pastry", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Soft ube roll with a molten cheese center.", 14, "/images/menu/14.webp", true, true, "Ube Cheese Pandesal", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, 70m, "Pastry", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Moist slice with toasted walnuts.", 15, "/images/menu/15.webp", true, false, "Banana Bread", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, 55m, "Pastry", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chewy, with dark chocolate chunks.", 16, "/images/menu/16.webp", true, false, "Choco Chip Cookie", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, 110m, "Snack", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Grilled sourdough, ham, melted cheese.", 17, "/images/menu/17.webp", true, false, "Ham & Cheese Toastie", 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "MenuItemCustomizations",
                columns: new[] { "CustomizationId", "MenuItemId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 8, 2 },
                    { 9, 2 },
                    { 10, 2 },
                    { 11, 2 },
                    { 12, 2 },
                    { 13, 2 },
                    { 14, 2 },
                    { 15, 2 },
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 },
                    { 4, 3 },
                    { 5, 3 },
                    { 6, 3 },
                    { 7, 3 },
                    { 8, 3 },
                    { 9, 3 },
                    { 10, 3 },
                    { 11, 3 },
                    { 12, 3 },
                    { 13, 3 },
                    { 14, 3 },
                    { 15, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 4 },
                    { 4, 4 },
                    { 5, 4 },
                    { 6, 4 },
                    { 7, 4 },
                    { 8, 4 },
                    { 9, 4 },
                    { 10, 4 },
                    { 11, 4 },
                    { 12, 4 },
                    { 13, 4 },
                    { 14, 4 },
                    { 15, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 5, 5 },
                    { 6, 5 },
                    { 7, 5 },
                    { 8, 5 },
                    { 9, 5 },
                    { 10, 5 },
                    { 11, 5 },
                    { 12, 5 },
                    { 13, 5 },
                    { 14, 5 },
                    { 15, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 6 },
                    { 4, 6 },
                    { 5, 6 },
                    { 6, 6 },
                    { 7, 6 },
                    { 8, 6 },
                    { 9, 6 },
                    { 10, 6 },
                    { 11, 6 },
                    { 12, 6 },
                    { 13, 6 },
                    { 14, 6 },
                    { 15, 6 },
                    { 1, 7 },
                    { 2, 7 },
                    { 3, 7 },
                    { 4, 7 },
                    { 8, 7 },
                    { 9, 7 },
                    { 10, 7 },
                    { 11, 7 },
                    { 12, 7 },
                    { 13, 7 },
                    { 14, 7 },
                    { 15, 7 },
                    { 1, 8 },
                    { 2, 8 },
                    { 3, 8 },
                    { 4, 8 },
                    { 5, 8 },
                    { 6, 8 },
                    { 7, 8 },
                    { 8, 8 },
                    { 9, 8 },
                    { 10, 8 },
                    { 15, 8 },
                    { 1, 9 },
                    { 2, 9 },
                    { 3, 9 },
                    { 4, 9 },
                    { 5, 9 },
                    { 6, 9 },
                    { 7, 9 },
                    { 8, 9 },
                    { 9, 9 },
                    { 10, 9 },
                    { 15, 9 },
                    { 1, 10 },
                    { 2, 10 },
                    { 3, 10 },
                    { 4, 10 },
                    { 5, 10 },
                    { 6, 10 },
                    { 7, 10 },
                    { 8, 10 },
                    { 9, 10 },
                    { 10, 10 },
                    { 15, 10 },
                    { 1, 11 },
                    { 2, 11 },
                    { 3, 11 },
                    { 4, 11 },
                    { 8, 11 },
                    { 9, 11 },
                    { 10, 11 },
                    { 1, 12 },
                    { 2, 12 },
                    { 3, 12 },
                    { 4, 12 },
                    { 8, 12 },
                    { 9, 12 },
                    { 10, 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customizations_Type_DisplayOrder",
                table: "Customizations",
                columns: new[] { "Type", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemCustomizations_CustomizationId",
                table: "MenuItemCustomizations",
                column: "CustomizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Category_IsAvailable",
                table: "MenuItems",
                columns: new[] { "Category", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Name",
                table: "MenuItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemCustomizations_CustomizationId",
                table: "OrderItemCustomizations",
                column: "CustomizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemId",
                table: "OrderItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemCustomizations");

            migrationBuilder.DropTable(
                name: "OrderItemCustomizations");

            migrationBuilder.DropTable(
                name: "Customizations");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
