using System.Net;
using ExpenseControl.Core.Common;

namespace ExpenseControl.Web.Services;

/// <summary>Builds the password reset e-mail from already translated texts.</summary>
public static class PasswordResetEmail
{
    /// <param name="link">Absolute reset URL (contains the raw token).</param>
    public static EmailMessage Build(string subject, string intro, string button, string expiry, string ignore, string link)
    {
        // Everything that ends up in the HTML is encoded, including the link: it is built from request data.
        var body = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 480px; margin: 0 auto; color: #0b0b0b;">
              <p>{WebUtility.HtmlEncode(intro)}</p>
              <p><a href="{WebUtility.HtmlEncode(link)}"
                    style="display: inline-block; padding: 10px 18px; background: #2a78d6; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{WebUtility.HtmlEncode(button)}</a></p>
              <p style="color: #52514e;">{WebUtility.HtmlEncode(expiry)}</p>
              <p style="color: #52514e;">{WebUtility.HtmlEncode(ignore)}</p>
            </div>
            """;

        return new EmailMessage(subject, body);
    }
}
