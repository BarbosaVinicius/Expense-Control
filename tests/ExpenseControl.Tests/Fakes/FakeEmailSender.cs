using ExpenseControl.Core.Abstractions;

namespace ExpenseControl.Tests.Fakes;

internal sealed class FakeEmailSender : IEmailSender
{
    public sealed record SentEmail(string To, string Subject, string Body);

    public List<SentEmail> Sent { get; } = [];

    public bool ShouldFail { get; set; }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("SMTP is down.");
        }

        Sent.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
