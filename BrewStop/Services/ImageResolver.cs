namespace BrewStop.Services;

public interface IImageResolver
{
    string Resolve(string? imageUrl, string? name = null);
}

public class ImageResolver(IHttpContextAccessor http) : IImageResolver
{
    private const string Placeholder = "images/placeholder.svg";

    private static string U(string id) =>
        $"https://images.unsplash.com/photo-{id}?auto=format&fit=crop&w=800&q=80";

    // Key = menu item / category name (case-insensitive)
    private static readonly Dictionary<string, string> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        // Categories
        ["Coffee"] = U("1561882468-9110e03e0f78"),
        ["Pastries"] = U("1699028057561-770e52cb572f"),

        // Drinks
        ["Caramel Latte"] = U("1534687941688-651ccaafbff8"),
        ["Caramel Cloud Latte"] = U("1563090308-5a7889e40542"),
        ["Velvet Mocha Brew"] = U("1598831745385-0c404c7034a9"),
        ["Vanilla Silk Macchiato"] = U("1631292007327-70297e83d78b"),
        ["Midnight Cold Brew"] = U("1517701550927-30cf4ba1dba5"),
        ["Hazelnut Dream Cappuccino"] = U("1561882468-9110e03e0f78"),

        // Pastries
        ["Butter Croissant"] = U("1555507036-ab1f4038808a"),
        ["Strawberry Crème Éclair"] = U("1721324390033-661a350668d6"),
        ["Golden Honey Puff"] = U("1587912001191-0cd4f14fd89e"),
        ["Midnight Cocoa Twist"] = U("1718897266472-5b7229ebdd3d"),
        ["Roselle Berry Tart"] = U("1649019937955-9687640aaaab"),
        ["Celestia Almond Croissant"] = U("1705856987588-1e6ae0b6061f"),
    };

    private string PathBase => http.HttpContext?.Request.PathBase.Value ?? string.Empty;

    public string Resolve(string? imageUrl, string? name = null)
    {
        // 1) Curated photo by item name
        if (!string.IsNullOrWhiteSpace(name) && Catalog.TryGetValue(name.Trim(), out var stock))
            return stock;

        // 2) Absolute http(s) URL stored in DB (e.g. added by an admin)
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            Uri.TryCreate(imageUrl, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return imageUrl;

        // 3) Placeholder
        return $"{PathBase}/{Placeholder}";
    }
}