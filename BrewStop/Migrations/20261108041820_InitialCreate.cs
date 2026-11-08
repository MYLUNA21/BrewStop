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
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Slug = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Group = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BadgeText = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Group", "ImageUrl", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, "Drinks", "/images/categories/coffee.jpg", "Coffee", "coffee" },
                    { 2, "Food", "/images/categories/pastries.jpg", "Pastries", "pastries" }
                });

            migrationBuilder.InsertData(
                table: "MenuItems",
                columns: new[] { "Id", "BadgeText", "CategoryId", "Description", "ImageUrl", "Name", "Price" },
                values: new object[,]
                {
                    { 1, "Bestseller", 1, "Espresso, steamed milk, and house caramel.", "/images/menu/caramel-latte.jpg", "Caramel Latte", 4.50m },
                    { 2, "Bestseller", 2, "Flaky, freshly baked all-butter croissant.", "/images/menu/butter-croissant.jpg", "Butter Croissant", 3.25m },
                    { 4, "Bestseller", 2, "Delicate choux pastry filled with smooth vanilla cream and topped with rich chocolate glaze.", "/images/menu/velvet-creme-eclair.jpg", "Velvet Crème Éclair", 3.95m },
                    { 5, "New", 2, "Flaky golden puff pastry filled with sweet honey custard and dusted with powdered sugar.", "/images/menu/golden-honey-puff.jpg", "Golden Honey Puff", 3.25m },
                    { 6, "Popular", 2, "Buttery twisted pastry layered with dark cocoa filling and finished with chocolate drizzle.", "/images/menu/midnight-cocoa-twist.jpg", "Midnight Cocoa Twist", 3.75m },
                    { 7, "Chef's Pick", 2, "Crispy buttery tart shell filled with creamy custard and topped with fresh mixed berries.", "/images/menu/roselle-berry-tart.jpg", "Roselle Berry Tart", 4.50m },
                    { 8, "Special", 2, "Golden flaky croissant filled with almond cream and topped with toasted almond slices.", "/images/menu/celestia-almond-croissant.jpg", "Celestia Almond Croissant", 4.25m },
                    { 9, "Bestseller", 1, "Smooth espresso blended with steamed milk and sweet caramel syrup, topped with creamy foam.", "/images/menu/caramel-cloud-latte.jpg", "Caramel Cloud Latte", 4.25m },
                    { 10, "Popular", 1, "Rich espresso mixed with premium chocolate and steamed milk, finished with chocolate powder.", "/images/menu/velvet-mocha-brew.jpg", "Velvet Mocha Brew", 4.50m },
                    { 11, "New", 1, "Bold espresso layered with creamy milk and sweet vanilla syrup for a smooth finish.", "/images/menu/vanilla-silk-macchiato.jpg", "Vanilla Silk Macchiato", 4.75m },
                    { 12, "Refreshing", 1, "Slow-steeped coffee served over ice with a naturally smooth flavor and refreshing finish.", "/images/menu/midnight-cold-brew.jpg", "Midnight Cold Brew", 3.95m },
                    { 13, "Special", 1, "Freshly brewed espresso combined with steamed milk, velvety foam, and roasted hazelnut syrup.", "/images/menu/hazelnut-dream-cappuccino.jpg", "Hazelnut Dream Cappuccino", 4.50m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId",
                table: "MenuItems",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
