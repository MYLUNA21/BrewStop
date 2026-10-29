using BrewStop.Models;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Data;

public class BrewStopDbContext : DbContext
{
    public BrewStopDbContext(DbContextOptions<BrewStopDbContext> options) : base(options) { }

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customization> Customizations => Set<Customization>();
    public DbSet<OrderItemCustomization> OrderItemCustomizations => Set<OrderItemCustomization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Full Unicode support (emoji in notes, Filipino names with diacritics).
        modelBuilder.UseCollation("utf8mb4_unicode_ci").HasCharSet("utf8mb4");

        // ---------- MenuItem ----------
        modelBuilder.Entity<MenuItem>(e =>
        {
            e.Property(m => m.Category).HasConversion<string>().HasMaxLength(20);
        });

        // ---------- Customization ----------
        modelBuilder.Entity<Customization>(e =>
        {
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        });

        // ---------- MenuItem <-> Customization (many-to-many, explicit join table) ----------
        modelBuilder.Entity<MenuItem>()
            .HasMany(m => m.Customizations)
            .WithMany(c => c.MenuItems)
            .UsingEntity<Dictionary<string, object>>(
                "MenuItemCustomization",
                right => right.HasOne<Customization>().WithMany().HasForeignKey("CustomizationId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<MenuItem>().WithMany().HasForeignKey("MenuItemId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("MenuItemCustomizations");
                    join.HasKey("MenuItemId", "CustomizationId");
                    join.HasData(BuildMenuItemCustomizationLinks());
                });

        // ---------- Order ----------
        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);

            e.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- OrderItem ----------
        modelBuilder.Entity<OrderItem>(e =>
        {
            // Never delete a menu item that has order history; mark it unavailable instead.
            e.HasOne(i => i.MenuItem)
                .WithMany(m => m.OrderItems)
                .HasForeignKey(i => i.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- OrderItemCustomization ----------
        modelBuilder.Entity<OrderItemCustomization>(e =>
        {
            e.HasKey(x => new { x.OrderItemId, x.CustomizationId });

            e.HasOne(x => x.OrderItem)
                .WithMany(i => i.SelectedCustomizations)
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Customization)
                .WithMany(c => c.OrderItemCustomizations)
                .HasForeignKey(x => x.CustomizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Seed data ----------
        modelBuilder.Entity<Customization>().HasData(SeedCustomizations);
        modelBuilder.Entity<MenuItem>().HasData(SeedMenu);
    }

    // =====================================================================
    //  Auditing
    // =====================================================================
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    // =====================================================================
    //  Seed data (static + deterministic so migrations stay stable)
    // =====================================================================
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Customization ids: 1-2 Size | 3-4 Temperature | 5-7 Milk | 8-10 Sweetness | 11-15 Add-ons
    private static readonly Customization[] SeedCustomizations =
    {
        new() { Id = 1,  Name = "Regular",          Type = CustomizationType.Size,        PriceAdjustment = 0,  IsDefault = true, DisplayOrder = 1 },
        new() { Id = 2,  Name = "Large",            Type = CustomizationType.Size,        PriceAdjustment = 30, DisplayOrder = 2 },
        new() { Id = 3,  Name = "Hot",              Type = CustomizationType.Temperature, PriceAdjustment = 0,  IsDefault = true, DisplayOrder = 1 },
        new() { Id = 4,  Name = "Iced",             Type = CustomizationType.Temperature, PriceAdjustment = 0,  DisplayOrder = 2 },
        new() { Id = 5,  Name = "Whole Milk",       Type = CustomizationType.Milk,        PriceAdjustment = 0,  IsDefault = true, DisplayOrder = 1 },
        new() { Id = 6,  Name = "Oat Milk",         Type = CustomizationType.Milk,        PriceAdjustment = 40, DisplayOrder = 2 },
        new() { Id = 7,  Name = "Almond Milk",      Type = CustomizationType.Milk,        PriceAdjustment = 40, DisplayOrder = 3 },
        new() { Id = 8,  Name = "No Sugar",         Type = CustomizationType.Sweetness,   PriceAdjustment = 0,  DisplayOrder = 1 },
        new() { Id = 9,  Name = "Half Sweet",       Type = CustomizationType.Sweetness,   PriceAdjustment = 0,  DisplayOrder = 2 },
        new() { Id = 10, Name = "Full Sweet",       Type = CustomizationType.Sweetness,   PriceAdjustment = 0,  IsDefault = true, DisplayOrder = 3 },
        new() { Id = 11, Name = "Extra Espresso Shot", Type = CustomizationType.AddOn,    PriceAdjustment = 30, DisplayOrder = 1 },
        new() { Id = 12, Name = "Vanilla Syrup",    Type = CustomizationType.AddOn,       PriceAdjustment = 20, DisplayOrder = 2 },
        new() { Id = 13, Name = "Caramel Syrup",    Type = CustomizationType.AddOn,       PriceAdjustment = 20, DisplayOrder = 3 },
        new() { Id = 14, Name = "Hazelnut Syrup",   Type = CustomizationType.AddOn,       PriceAdjustment = 20, DisplayOrder = 4 },
        new() { Id = 15, Name = "Whipped Cream",    Type = CustomizationType.AddOn,       PriceAdjustment = 20, DisplayOrder = 5 },
    };

    private static readonly MenuItem[] SeedMenu =
    {
        Item(1,  "Espresso",              "Double shot, rich crema.",                       ItemCategory.Coffee,    80,  3, false, 1),
        Item(2,  "Americano",             "Espresso over water. Clean and bold.",           ItemCategory.Coffee,    100, 3, false, 2),
        Item(3,  "Caffè Latte",           "Espresso with steamed milk.",                    ItemCategory.Coffee,    130, 5, false, 3),
        Item(4,  "Cappuccino",            "Equal parts espresso, milk, and foam.",          ItemCategory.Coffee,    130, 5, false, 4),
        Item(5,  "Spanish Latte",         "Sweetened condensed milk meets espresso.",       ItemCategory.Coffee,    150, 5, true,  5),
        Item(6,  "Caramel Macchiato",     "Vanilla, steamed milk, espresso, caramel drizzle.", ItemCategory.Coffee, 160, 6, false, 6),
        Item(7,  "Sea Salt Cold Brew",    "18-hour cold brew topped with sea salt foam.",   ItemCategory.Coffee,    140, 4, true,  7),
        Item(8,  "Matcha Latte",          "Ceremonial-grade matcha with milk.",             ItemCategory.NonCoffee, 150, 5, true,  8),
        Item(9,  "Strawberry Milk",       "Fresh strawberry purée and cold milk.",          ItemCategory.NonCoffee, 120, 4, false, 9),
        Item(10, "Dark Chocolate",        "Rich cocoa, steamed to order.",                  ItemCategory.NonCoffee, 120, 4, false, 10),
        Item(11, "Calamansi Honey Tea",   "Fresh calamansi and local honey.",               ItemCategory.Tea,       90,  4, false, 11),
        Item(12, "Iced Lemon Tea",        "Black tea shaken with lemon.",                   ItemCategory.Tea,       85,  3, false, 12),
        Item(13, "Butter Croissant",      "Flaky, baked fresh each morning.",               ItemCategory.Pastry,    85,  2, false, 13),
        Item(14, "Ube Cheese Pandesal",   "Soft ube roll with a molten cheese center.",     ItemCategory.Pastry,    60,  2, true,  14),
        Item(15, "Banana Bread",          "Moist slice with toasted walnuts.",              ItemCategory.Pastry,    70,  2, false, 15),
        Item(16, "Choco Chip Cookie",     "Chewy, with dark chocolate chunks.",             ItemCategory.Pastry,    55,  1, false, 16),
        Item(17, "Ham & Cheese Toastie",  "Grilled sourdough, ham, melted cheese.",         ItemCategory.Snack,     110, 6, false, 17),
    };

    // Items that are served black: no milk options.
    private static readonly HashSet<int> NoMilkItemIds = new() { 1, 2, 7 };

    private static MenuItem Item(int id, string name, string description, ItemCategory category,
                                 decimal price, int prepMinutes, bool featured, int order) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        Category = category,
        BasePrice = price,
        ImageUrl = $"/images/menu/{id:00}.webp",
        IsAvailable = true,
        IsFeatured = featured,
        PrepTimeMinutes = prepMinutes,
        DisplayOrder = order,
        CreatedAt = SeedTimestamp,
        UpdatedAt = SeedTimestamp
    };

    /// <summary>Which customization options apply to which menu items, derived from category.</summary>
    private static IEnumerable<object> BuildMenuItemCustomizationLinks()
    {
        var links = new List<object>();

        foreach (var item in SeedMenu)
        {
            var ids = new List<int>();

            switch (item.Category)
            {
                case ItemCategory.Coffee:
                    ids.AddRange(new[] { 1, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14, 15 });
                    if (!NoMilkItemIds.Contains(item.Id)) ids.AddRange(new[] { 5, 6, 7 });
                    break;
                case ItemCategory.NonCoffee:
                    ids.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15 });
                    break;
                case ItemCategory.Tea:
                    ids.AddRange(new[] { 1, 2, 3, 4, 8, 9, 10 });
                    break;
                // Pastry and Snack: no customizations.
            }

            foreach (var customizationId in ids)
                links.Add(new { MenuItemId = item.Id, CustomizationId = customizationId });
        }

        return links;
    }
}
