using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace ExpenseControl.Core.Domain;

/// <summary>Rules for new accounts, shared by the services and the UI.</summary>
public static partial class AccountRules
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 50;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 100;

    public const string UsernamePattern = "^[A-Za-z0-9._-]+$";

    public const string UsernameFormatMessage = "Username may only contain letters, digits, dots, underscores and hyphens.";

    [GeneratedRegex(UsernamePattern)]
    private static partial Regex UsernameRegex();

    public static bool IsValidUsername([NotNullWhen(true)] string? username) =>
        username is { Length: >= MinUsernameLength and <= MaxUsernameLength } && UsernameRegex().IsMatch(username);

    public static bool IsValidPassword([NotNullWhen(true)] string? password) =>
        password is { Length: >= MinPasswordLength and <= MaxPasswordLength };

    public static bool IsValidEmail([NotNullWhen(true)] string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxEmailLength)
        {
            return false;
        }

        try
        {
            return new MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
