using Aib.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aib.Infrastructure.Email;

/// <summary>Development email sender: logs the message instead of delivering it.</summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] To: {To} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
