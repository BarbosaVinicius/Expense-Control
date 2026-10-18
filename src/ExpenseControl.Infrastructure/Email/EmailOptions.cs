namespace ExpenseControl.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// When false (the default) no e-mail is sent; messages are written to the log instead.
    /// Handy for local development, when no SMTP account is configured.
    /// </summary>
    public bool Enabled { get; set; }

    public string SmtpServer { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string FromEmail { get; set; } = string.Empty;

    public string FromPassword { get; set; } = string.Empty;
}
