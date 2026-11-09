using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace BrewStop.Services;

public class CaptchaOptions
{
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}

public interface ICaptchaService
{

    Task<bool> VerifyAsync(HttpRequest request, string expectedAction, CancellationToken ct = default);
}

public class CaptchaService(HttpClient http, IOptions<CaptchaOptions> options, ILogger<CaptchaService> log)
    : ICaptchaService
{
    public const string FormField = "cf-turnstile-response";   // injected into the form by the widget
    private readonly CaptchaOptions _o = options.Value;

    public async Task<bool> VerifyAsync(HttpRequest request, string expectedAction, CancellationToken ct = default)
    {
        var token = request.HasFormContentType ? request.Form[FormField].ToString() : string.Empty;

        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            log.LogWarning("Captcha token missing or malformed for action {Action}", expectedAction);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_o.SecretKey))
        {
            log.LogError("Captcha:SecretKey is not configured; rejecting request.");
            return false;
        }

        var form = new Dictionary<string, string>
        {
            ["secret"] = _o.SecretKey,
            ["response"] = token
        };
        var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString();
        if (ip is not null) form["remoteip"] = ip;

        try
        {
            using var resp = await http.PostAsync(_o.VerifyUrl, new FormUrlEncodedContent(form), ct);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogError("Captcha verify endpoint returned {Status}", (int)resp.StatusCode);
                return false;
            }

            var result = await resp.Content.ReadFromJsonAsync<TurnstileResult>(cancellationToken: ct);
            if (result is null || !result.Success)
            {
                log.LogWarning("Captcha rejected for {Action}: {Codes}", expectedAction,
                    string.Join(",", result?.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }


            if (!string.IsNullOrEmpty(result.Action) &&
                !string.Equals(result.Action, expectedAction, StringComparison.OrdinalIgnoreCase))
            {
                log.LogWarning("Captcha action mismatch: expected {Expected}, got {Actual}", expectedAction, result.Action);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogError(ex, "Captcha verification failed to complete");
            return false;
        }
    }

    private sealed class TurnstileResult
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
    }
}