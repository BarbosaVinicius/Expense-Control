using ExpenseControl.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseControl.Infrastructure.Email;

/// <summary>Stand-in used when SMTP is not configured: writes the message to the log instead of sending it.</summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogWarning("E-mail is disabled. Would send to {To} with subject '{Subject}':{NewLine}{Body}",
                toEmail, subject, Environment.NewLine, htmlBody);
        }
        else
        {
            // The body can contain secrets such as reset links, so it is only logged during development.
            logger.LogWarning("E-mail is disabled. Message to {To} with subject '{Subject}' was not sent.", toEmail, subject);
        }

        return Task.CompletedTask;
    }
}
