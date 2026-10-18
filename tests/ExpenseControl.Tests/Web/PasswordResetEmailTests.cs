using ExpenseControl.Web.Services;

namespace ExpenseControl.Tests.Web;

public class PasswordResetEmailTests
{
    [Fact]
    public void The_link_is_html_encoded_so_it_cannot_break_out_of_the_attribute()
    {
        var message = PasswordResetEmail.Build("Subject", "Intro", "Reset", "Valid 60 minutes", "Ignore",
            "https://x/reset?token=abc&a=\"><script>alert(1)</script>");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&amp;a=&quot;&gt;&lt;script&gt;", message.HtmlBody);
        Assert.Equal("Subject", message.Subject);
    }

    [Fact]
    public void Translated_texts_are_encoded_too()
    {
        var message = PasswordResetEmail.Build("S", "<b>Hi</b> & welcome", "Go <now>", "e", "i", "https://x/");

        Assert.DoesNotContain("<b>Hi</b>", message.HtmlBody);
        Assert.Contains("&lt;b&gt;Hi&lt;/b&gt; &amp; welcome", message.HtmlBody);
        Assert.Contains("Go &lt;now&gt;", message.HtmlBody);
    }

    [Fact]
    public void The_message_contains_every_part()
    {
        var message = PasswordResetEmail.Build("S", "Intro text", "Button text", "Expiry text", "Ignore text", "https://example.com/r?token=t");

        Assert.All(new[] { "Intro text", "Button text", "Expiry text", "Ignore text", "https://example.com/r?token=t" },
            part => Assert.Contains(part, message.HtmlBody));
    }
}
