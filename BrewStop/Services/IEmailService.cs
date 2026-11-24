using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace BrewStop.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}

// Sends via Gmail SMTP when Smtp:Email + Smtp:AppPassword are set; otherwise logs the email to the console.
public class LoggingEmailService(IConfiguration config, ILogger<LoggingEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var from = config["Smtp:Email"];
        var password = config["Smtp:AppPassword"];

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(password))
        {
            LogToConsole(to, subject, htmlBody);
            return;
        }

        try
        {
            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(from.Trim(), password.Replace(" ", "")),
                Timeout = 15000
            };
            using var message = new MailMessage
            {
                From = new MailAddress(from.Trim(), "BrewStop"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message);
            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP send to {To} failed; falling back to console", to);
            LogToConsole(to, subject, htmlBody);
        }
    }

    private void LogToConsole(string to, string subject, string htmlBody)
    {
        var match = Regex.Match(htmlBody, "href=\"([^\"]+)\"");
        var detail = match.Success
            ? "LINK: " + WebUtility.HtmlDecode(match.Groups[1].Value)
            : "BODY: " + WebUtility.HtmlDecode(Regex.Replace(htmlBody, "<[^>]+>", " ")).Trim();

        logger.LogWarning("\n===== EMAIL to {To} =====\nSubject: {Subject}\n{Detail}\n=========================",
            to, subject, detail);
    }
}