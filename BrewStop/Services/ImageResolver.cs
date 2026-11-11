using System.Collections.Concurrent;

namespace BrewStop.Services;

public interface IImageResolver
{
    string Resolve(string? imageUrl, string? name = null);
}

public class ImageResolver(IWebHostEnvironment env, IHttpContextAccessor http) : IImageResolver
{
    private const string Placeholder = "images/placeholder.svg";
    private static readonly string[] Exts = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg"];

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

    private readonly ConcurrentDictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private string PathBase => http.HttpContext?.Request.PathBase.Value ?? string.Empty;

    public string Resolve(string? imageUrl, string? name = null)
    {
        // 1) Curated stock photo by name
        if (!string.IsNullOrWhiteSpace(name) && Catalog.TryGetValue(name.Trim(), out var stock))
            return stock;

        // 2) Already an absolute http(s) URL in the DB
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            Uri.TryCreate(imageUrl, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return imageUrl;

        // 3) Local wwwroot/images file, else placeholder
        var rel = Lookup(imageUrl) ?? Placeholder;
        var encoded = string.Join('/', rel.Split('/').Select(Uri.EscapeDataString));
        return $"{PathBase}/{encoded}";
    }

    private string? Lookup(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var clean = Uri.UnescapeDataString(imageUrl.Split('?', '#')[0]).Replace('\\', '/');
        var fileName = Path.GetFileName(clean);
        if (string.IsNullOrEmpty(fileName)) return null;

        var key = Path.GetFileNameWithoutExtension(fileName);
        if (_files.TryGetValue(key, out var hit)) return hit;

        Scan();
        return _files.TryGetValue(key, out hit) ? hit : null;
    }

    private void Scan()
    {
        lock (_lock)
        {
            var root = Path.Combine(env.WebRootPath, "images");
            if (!Directory.Exists(root)) return;

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!Exts.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                var rel = "images/" + Path.GetRelativePath(root, file).Replace('\\', '/');
                _files[Path.GetFileNameWithoutExtension(file)] = rel;
            }
        }
    }
}