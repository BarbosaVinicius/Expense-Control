using System.Net;
using System.Net.Mail;
using ExpenseControl.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace ExpenseControl.Infrastructure.Email;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpServer) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            throw new InvalidOperationException(
                $"{EmailOptions.SectionName}:SmtpServer and {EmailOptions.SectionName}:FromEmail must be set when e-mail is enabled.");
        }

        using var client = new SmtpClient(settings.SmtpServer, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = new NetworkCredential(settings.FromEmail, settings.FromPassword)
        };

        using var message = new MailMessage(new MailAddress(settings.FromEmail), new MailAddress(toEmail))
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
