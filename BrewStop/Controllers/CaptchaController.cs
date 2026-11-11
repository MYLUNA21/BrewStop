using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace BrewStop.Controllers;

[AllowAnonymous, Route("captcha")]
public class CaptchaController(ICaptchaService captcha, IOptions<CaptchaOptions> options) : Controller
{
    public sealed record VerifyRequest(string ChallengeId, string[] Tiles, string Action);

    private static readonly FileExtensionContentTypeProvider Mime = new();

    [HttpGet("challenge")]
    public IActionResult Challenge([FromQuery] string action)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(action) || action.Length > 32) return BadRequest();

        var ch = captcha.CreateChallenge(action);
        if (ch is null) return StatusCode(503);

        return Json(new
        {
            id = ch.Id,
            tiles = ch.Tiles.Select(t => new { id = t.Id, src = $"{Url.Content("~/captcha/img")}/{ch.Id}/{t.Id}" })
        });
    }

    [HttpGet("img/{challengeId}/{tileId}")]
    public IActionResult Image(string challengeId, string tileId)
    {
        var file = captcha.GetTileFile(challengeId, tileId);
        if (file is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        Mime.TryGetContentType(file, out var type);
        return PhysicalFile(file, type ?? "application/octet-stream");
    }

    [HttpPost("verify")]
    public IActionResult Verify([FromBody] VerifyRequest r)
    {
        Response.Headers.CacheControl = "no-store";
        if (r?.Tiles is null || r.Tiles.Length > 9) return BadRequest();

        var token = captcha.Solve(r.ChallengeId, r.Tiles, r.Action);
        return token is null
            ? Ok(new { ok = false })
            : Ok(new { ok = true, token, expiresIn = options.Value.TokenMinutes * 60 });
    }
}