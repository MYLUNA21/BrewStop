using BrewStop.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrewStop.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        await EnsureUserAsync(users, log, config["Seed:AdminEmail"], config["Seed:AdminPassword"],
            "BrewStop Admin", "ADMIN-001", AppRoles.Admin);
        await EnsureUserAsync(users, log, config["Seed:BaristaEmail"], config["Seed:BaristaPassword"],
            "BrewStop Barista", "BARISTA-001", AppRoles.Barista);

        await EnsureTestItemsAsync(db);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, ILogger log,
        string? email, string? password, string fullName, string campusId, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CampusId = campusId
        };

        var result = await users.CreateAsync(user, password);
        if (result.Succeeded) await users.AddToRoleAsync(user, role);
        else log.LogError("{Role} seed failed: {Errors}", role, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // TC-04: sold-out item (stock 0). TC-10: exactly one left (stock 1). Created once; admins can restock later.
    private static async Task EnsureTestItemsAsync(ApplicationDbContext db)
    {
        var coffee = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "coffee");
        if (coffee is null) return;

        var wanted = new (string Name, string Description, decimal Price, int Stock)[]
        {
            ("Iced Spanish Latte", "Espresso over ice with sweetened condensed milk.", 48.50m, 0),
            ("Caramel Macchiato", "Vanilla milk marked with espresso and caramel drizzle.", 49.50m, 1)
        };

        foreach (var w in wanted)
        {
            if (await db.MenuItems.AnyAsync(m => m.Name == w.Name)) continue;

            var item = new MenuItem
            {
                Name = w.Name,
                Description = w.Description,
                Price = w.Price,
                BadgeText = "New",
                CategoryId = coffee.Id
            };
            db.MenuItems.Add(item);
            db.InventoryItems.Add(new InventoryItem { MenuItem = item, Quantity = w.Stock });
        }

        await db.SaveChangesAsync();
    }
}