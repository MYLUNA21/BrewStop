using BrewStop.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

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

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Slug = "coffee", Name = "Coffee", Group = "Drinks", ImageUrl = "/images/categories/coffee.jpg" },
            new Category { Id = 2, Slug = "pastries", Name = "Pastries", Group = "Food", ImageUrl = "/images/categories/pastries.jpg" }
        );


        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem
            {
                Id = 1,
                Name = "Caramel Latte",
                Description = "Espresso, steamed milk, and house caramel.",
                Price = 4.50m,
                ImageUrl = "/images/menu/caramel-latte.jpg",
                BadgeText = "Bestseller",
                CategoryId = 1
            },
            new MenuItem
            {
                Id = 2,
                Name = "Butter Croissant",
                Description = "Flaky, freshly baked all-butter croissant.",
                Price = 3.25m,
                ImageUrl = "/images/menu/butter-croissant.jpg",
                BadgeText = "Bestseller",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 4,
                Name = "Velvet Crème Éclair",
                Description = "Delicate choux pastry filled with smooth vanilla cream and topped with rich chocolate glaze.",
                Price = 3.95m,
                ImageUrl = "/images/menu/velvet-creme-eclair.jpg",
                BadgeText = "Bestseller",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 5,
                Name = "Golden Honey Puff",
                Description = "Flaky golden puff pastry filled with sweet honey custard and dusted with powdered sugar.",
                Price = 3.25m,
                ImageUrl = "/images/menu/golden-honey-puff.jpg",
                BadgeText = "New",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 6,
                Name = "Midnight Cocoa Twist",
                Description = "Buttery twisted pastry layered with dark cocoa filling and finished with chocolate drizzle.",
                Price = 3.75m,
                ImageUrl = "/images/menu/midnight-cocoa-twist.jpg",
                BadgeText = "Popular",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 7,
                Name = "Roselle Berry Tart",
                Description = "Crispy buttery tart shell filled with creamy custard and topped with fresh mixed berries.",
                Price = 4.50m,
                ImageUrl = "/images/menu/roselle-berry-tart.jpg",
                BadgeText = "Chef's Pick",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 8,
                Name = "Celestia Almond Croissant",
                Description = "Golden flaky croissant filled with almond cream and topped with toasted almond slices.",
                Price = 4.25m,
                ImageUrl = "/images/menu/celestia-almond-croissant.jpg",
                BadgeText = "Special",
                CategoryId = 2
            },
            new MenuItem
            {
                Id = 9,
                Name = "Caramel Cloud Latte",
                Description = "Smooth espresso blended with steamed milk and sweet caramel syrup, topped with creamy foam.",
                Price = 4.25m,
                ImageUrl = "/images/menu/caramel-cloud-latte.jpg",
                BadgeText = "Bestseller",
                CategoryId = 1
            },
            new MenuItem
            {
                Id = 10,
                Name = "Velvet Mocha Brew",
                Description = "Rich espresso mixed with premium chocolate and steamed milk, finished with chocolate powder.",
                Price = 4.50m,
                ImageUrl = "/images/menu/velvet-mocha-brew.jpg",
                BadgeText = "Popular",
                CategoryId = 1
            },
            new MenuItem
            {
                Id = 11,
                Name = "Vanilla Silk Macchiato",
                Description = "Bold espresso layered with creamy milk and sweet vanilla syrup for a smooth finish.",
                Price = 4.75m,
                ImageUrl = "/images/menu/vanilla-silk-macchiato.jpg",
                BadgeText = "New",
                CategoryId = 1
            },
            new MenuItem
            {
                Id = 12,
                Name = "Midnight Cold Brew",
                Description = "Slow-steeped coffee served over ice with a naturally smooth flavor and refreshing finish.",
                Price = 3.95m,
                ImageUrl = "/images/menu/midnight-cold-brew.jpg",
                BadgeText = "Refreshing",
                CategoryId = 1
            },
            new MenuItem
            {
                Id = 13,
                Name = "Hazelnut Dream Cappuccino",
                Description = "Freshly brewed espresso combined with steamed milk, velvety foam, and roasted hazelnut syrup.",
                Price = 4.50m,
                ImageUrl = "/images/menu/hazelnut-dream-cappuccino.jpg",
                BadgeText = "Special",
                CategoryId = 1
            }
        );
    }
}