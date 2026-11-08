using System.Net;
using System.Text.RegularExpressions;

namespace BrewStop.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}

// Dev only: prints the email to the console. Replace with an SMTP version later.
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody)
    {
        var match = Regex.Match(htmlBody, "href=\"([^\"]+)\"");
        var link = match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : "(no link)";

        logger.LogWarning("\n===== EMAIL to {To} =====\nSubject: {Subject}\nLINK: {Link}\n=========================",
            to, subject, link);
        return Task.CompletedTask;
    }
}