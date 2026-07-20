using System.Net;
using System.Net.Mail;
using Aib.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<MailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var mail = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(mail.From, mail.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(mail.Host, mail.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = IsSsl(mail.Encryption),
            // Mailpit and similar local relays accept unauthenticated SMTP.
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(mail.Username))
            client.Credentials = new NetworkCredential(mail.Username, mail.Password ?? "");

        logger.LogInformation("Sending email to {To} via {Host}:{Port}", toEmail, mail.Host, mail.Port);
        await client.SendMailAsync(message, ct);
    }

    private static bool IsSsl(string? encryption)
    {
        if (string.IsNullOrWhiteSpace(encryption)) return false;
        return encryption is "ssl" or "tls" or "starttls"
            || string.Equals(encryption, "true", StringComparison.OrdinalIgnoreCase);
    }
}
