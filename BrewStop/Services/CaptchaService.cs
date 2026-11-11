using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BrewStop.Services;

public class CaptchaOptions
{
    public string ImageFolder { get; set; } = "images/captcha";
    public int MinCoffee { get; set; } = 2;
    public int MaxCoffee { get; set; } = 4;
    public int ChallengeMinutes { get; set; } = 5;
    public int TokenMinutes { get; set; } = 10;
}

public sealed record CaptchaTile(string Id, string File, bool IsCoffee);
public sealed record CaptchaChallenge(string Id, string Action, IReadOnlyList<CaptchaTile> Tiles);

public interface ICaptchaService
{
    CaptchaChallenge? CreateChallenge(string action);
    string? GetTileFile(string challengeId, string tileId);
    string? Solve(string challengeId, IEnumerable<string> selectedTileIds, string action);
    Task<bool> VerifyAsync(HttpRequest request, string expectedAction, CancellationToken ct = default);
}

public class CaptchaService(
    IMemoryCache cache,
    IOptions<CaptchaOptions> options,
    IWebHostEnvironment env,
    ILogger<CaptchaService> log) : ICaptchaService
{
    public const string FormField = "bs-captcha-token";
    private static readonly string[] Ext = ["*.jpg", "*.jpeg", "*.png", "*.webp"];
    private readonly CaptchaOptions _o = options.Value;

    private static string ChKey(string id) => "captcha:ch:" + id;
    private static string TokKey(string t) => "captcha:tok:" + t;
    private static string NewId(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));

    public CaptchaChallenge? CreateChallenge(string action)
    {
        var root = Path.Combine(env.WebRootPath, _o.ImageFolder);

        // FIX: Search the "coffee" subfolder AND the root folder for files starting with "coffee"
        var coffee = List(Path.Combine(root, "coffee"))
            .Concat(List(root).Where(f => Path.GetFileName(f).StartsWith("coffee", StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        var other = List(Path.Combine(root, "other"));

        if (coffee.Count < _o.MinCoffee || other.Count == 0)
        {
            log.LogError("Captcha images missing in {Root}: coffee={C} (need >= {MinC}), other={O} (need >= 1)",
                root, coffee.Count, _o.MinCoffee, other.Count);
            return null;
        }

        var coffeeCount = Math.Min(RandomNumberGenerator.GetInt32(_o.MinCoffee, _o.MaxCoffee + 1), coffee.Count);
        var otherCount = 9 - coffeeCount;

        var otherPool = Shuffle(other);
        var otherPicks = Enumerable.Range(0, otherCount).Select(i => otherPool[i % otherPool.Count]);

        var tiles = Shuffle(coffee).Take(coffeeCount).Select(f => new CaptchaTile(NewId(8), f, true))
            .Concat(otherPicks.Select(f => new CaptchaTile(NewId(8), f, false)))
            .ToList();
        Shuffle(tiles);

        var ch = new CaptchaChallenge(NewId(16), action, tiles);
        cache.Set(ChKey(ch.Id), ch, TimeSpan.FromMinutes(_o.ChallengeMinutes));
        return ch;
    }

    public string? GetTileFile(string challengeId, string tileId) =>
        cache.TryGetValue(ChKey(challengeId), out CaptchaChallenge? ch)
            ? ch?.Tiles.FirstOrDefault(t => t.Id == tileId)?.File
            : null;

    public string? Solve(string challengeId, IEnumerable<string> selectedTileIds, string action)
    {
        if (!cache.TryGetValue(ChKey(challengeId), out CaptchaChallenge? ch) || ch is null) return null;
        cache.Remove(ChKey(challengeId));

        if (!string.Equals(ch.Action, action, StringComparison.OrdinalIgnoreCase)) return null;

        var picked = selectedTileIds.ToHashSet();
        var correct = ch.Tiles.Where(t => t.IsCoffee).Select(t => t.Id).ToHashSet();
        if (!picked.SetEquals(correct)) return null;

        var token = NewId(24);
        cache.Set(TokKey(token), ch.Action, TimeSpan.FromMinutes(_o.TokenMinutes));
        return token;
    }

    public Task<bool> VerifyAsync(HttpRequest request, string expectedAction, CancellationToken ct = default)
    {
        var token = request.HasFormContentType ? request.Form[FormField].ToString() : string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            !cache.TryGetValue(TokKey(token), out string? action))
        {
            log.LogWarning("Captcha token missing/invalid for {Action}", expectedAction);
            return Task.FromResult(false);
        }

        cache.Remove(TokKey(token));
        return Task.FromResult(string.Equals(action, expectedAction, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> List(string dir) =>
        Directory.Exists(dir) ? Ext.SelectMany(e => Directory.EnumerateFiles(dir, e)).ToList() : [];

    private static List<T> Shuffle<T>(List<T> src)
    {
        for (var i = src.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (src[i], src[j]) = (src[j], src[i]);
        }
        return src;
    }
}