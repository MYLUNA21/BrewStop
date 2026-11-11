using BrewStop.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ItemSize> ItemSizes => Set<ItemSize>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemAddOn> OrderItemAddOns => Set<OrderItemAddOn>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.CampusId)
            .IsUnique();

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasMany(c => c.MenuItems)
             .WithOne(m => m.Category)
             .HasForeignKey(m => m.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MenuItem>(e =>
        {
            e.Property(m => m.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<ItemSize>(e =>
        {
            e.Property(s => s.PriceDelta).HasPrecision(10, 2);
            e.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<AddOn>(e =>
        {
            e.Property(a => a.Price).HasPrecision(10, 2);
            e.HasIndex(a => new { a.Group, a.Name }).IsUnique();
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Total).HasPrecision(10, 2);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(o => new { o.UserId, o.CreatedAtUtc });
            e.HasIndex(o => new { o.Status, o.PickupAtUtc });
            e.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(10, 2);
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.MenuItem).WithMany().HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItemAddOn>(e =>
        {
            e.Property(a => a.Price).HasPrecision(10, 2);
            e.HasOne(a => a.OrderItem).WithMany(i => i.AddOns).HasForeignKey(a => a.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AddOn).WithMany().HasForeignKey(a => a.AddOnId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.HasIndex(i => i.MenuItemId).IsUnique();
            e.HasOne(i => i.MenuItem).WithOne().HasForeignKey<InventoryItem>(i => i.MenuItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_InventoryItems_Quantity", "`Quantity` >= 0"));
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAtUtc);
            e.HasIndex(a => a.UserId);
        });

        // ---------- Seed data ----------

        modelBuilder.Entity<ItemSize>().HasData(
            new ItemSize { Id = 1, Name = "Small", PriceDelta = 0.00m, SortOrder = 1 },
            new ItemSize { Id = 2, Name = "Medium", PriceDelta = 0.50m, SortOrder = 2 },
            new ItemSize { Id = 3, Name = "Large", PriceDelta = 1.00m, SortOrder = 3 }
        );

        modelBuilder.Entity<AddOn>().HasData(
            new AddOn { Id = 1, Name = "Extra Espresso Shot", Price = 0.75m, Group = "Drinks" },
            new AddOn { Id = 2, Name = "Oat Milk", Price = 0.60m, Group = "Drinks" },
            new AddOn { Id = 3, Name = "Whipped Cream", Price = 0.40m, Group = "Drinks" },
            new AddOn { Id = 4, Name = "Vanilla Syrup", Price = 0.50m, Group = "Drinks" },
            new AddOn { Id = 5, Name = "Caramel Drizzle", Price = 0.50m, Group = "Drinks" },
            new AddOn { Id = 6, Name = "Warm It Up", Price = 0.00m, Group = "Food" }
        );

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Slug = "coffee", Name = "Coffee", Group = "Drinks", ImageUrl = "" },
            new Category { Id = 2, Slug = "pastries", Name = "Pastries", Group = "Food", ImageUrl = "" }
        );

        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem { Id = 1, Name = "Caramel Latte", Description = "Espresso, steamed milk, and house caramel.", Price = 50.50m, BadgeText = "Bestseller", CategoryId = 1 },
            new MenuItem { Id = 2, Name = "Butter Croissant", Description = "Flaky, freshly baked all-butter croissant.", Price = 30.25m, BadgeText = "Bestseller", CategoryId = 2 },
            new MenuItem { Id = 4, Name = "Strawberry Crème Éclair", Description = "Delicate choux pastry filled with smooth vanilla cream and topped with rich strawberry glaze.", Price = 30.95m, ImageUrl = "/images/Strawberry Eclair.png", BadgeText = "Bestseller", CategoryId = 2 },
            new MenuItem { Id = 5, Name = "Golden Honey Puff", Description = "Flaky golden puff pastry filled with sweet honey custard and dusted with powdered sugar.", Price = 30.25m, BadgeText = "New", CategoryId = 2 },
            new MenuItem { Id = 6, Name = "Midnight Cocoa Twist", Description = "Buttery twisted pastry layered with dark cocoa filling and finished with chocolate drizzle.", Price = 35.75m, BadgeText = "Popular", CategoryId = 2 },
            new MenuItem { Id = 7, Name = "Roselle Berry Tart", Description = "Crispy buttery tart shell filled with creamy custard and topped with fresh mixed berries.", Price = 50.50m, BadgeText = "Chef's Pick", CategoryId = 2 },
            new MenuItem { Id = 8, Name = "Celestia Almond Croissant", Description = "Golden flaky croissant filled with almond cream and topped with toasted almond slices.", Price = 40.25m, BadgeText = "Special", CategoryId = 2 },
            new MenuItem { Id = 9, Name = "Caramel Cloud Latte", Description = "Smooth espresso blended with steamed milk and sweet caramel syrup, topped with creamy foam.", Price = 39.25m, BadgeText = "Bestseller", CategoryId = 1 },
            new MenuItem { Id = 10, Name = "Velvet Mocha Brew", Description = "Rich espresso mixed with premium chocolate and steamed milk, finished with chocolate powder.", Price = 39.50m, BadgeText = "Popular", CategoryId = 1 },
            new MenuItem { Id = 11, Name = "Vanilla Silk Macchiato", Description = "Bold espresso layered with creamy milk and sweet vanilla syrup for a smooth finish.", Price = 45.75m, BadgeText = "New", CategoryId = 1 },
            new MenuItem { Id = 12, Name = "Midnight Cold Brew", Description = "Slow-steeped coffee served over ice with a naturally smooth flavor and refreshing finish.", Price = 45.95m, BadgeText = "Refreshing", CategoryId = 1 },
            new MenuItem { Id = 13, Name = "Hazelnut Dream Cappuccino", Description = "Freshly brewed espresso combined with steamed milk, velvety foam, and roasted hazelnut syrup.", Price = 50.50m, BadgeText = "Special", CategoryId = 1 }
        );

        // Starting stock: coffee 50, pastries 20 (editable by admins later, FR-14/FR-15).
        modelBuilder.Entity<InventoryItem>().HasData(
            new InventoryItem { Id = 1, MenuItemId = 1, Quantity = 50 },
            new InventoryItem { Id = 2, MenuItemId = 2, Quantity = 20 },
            new InventoryItem { Id = 3, MenuItemId = 4, Quantity = 20 },
            new InventoryItem { Id = 4, MenuItemId = 5, Quantity = 20 },
            new InventoryItem { Id = 5, MenuItemId = 6, Quantity = 20 },
            new InventoryItem { Id = 6, MenuItemId = 7, Quantity = 20 },
            new InventoryItem { Id = 7, MenuItemId = 8, Quantity = 20 },
            new InventoryItem { Id = 8, MenuItemId = 9, Quantity = 50 },
            new InventoryItem { Id = 9, MenuItemId = 10, Quantity = 50 },
            new InventoryItem { Id = 10, MenuItemId = 11, Quantity = 50 },
            new InventoryItem { Id = 11, MenuItemId = 12, Quantity = 50 },
            new InventoryItem { Id = 12, MenuItemId = 13, Quantity = 50 }
        );
    }
}