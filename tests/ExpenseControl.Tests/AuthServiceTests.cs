using System.Net;
using ExpenseControl.Core.Common;
using ExpenseControl.Core.Services;
using ExpenseControl.Infrastructure.Security;
using ExpenseControl.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ExpenseControl.Tests;

public class AuthServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 10);
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(_users, _hasher, _email, _clock, NullLogger<AuthService>.Instance);
    }

    // ---- Registration ----

    [Fact]
    public async Task Register_creates_the_user_with_a_hashed_password()
    {
        var result = await _auth.RegisterAsync("maria", "maria@example.com", "s3cret-pass");

        Assert.True(result.Succeeded);
        var stored = _users.PasswordHashOf("maria");
        Assert.NotNull(stored);
        Assert.NotEqual("s3cret-pass", stored);
    }

    [Theory]
    [InlineData("ab", "a@example.com", "longenough")]            // username too short
    [InlineData("has space", "a@example.com", "longenough")]     // invalid username characters
    [InlineData("maria", "not-an-email", "longenough")]          // invalid e-mail
    [InlineData("maria", "a@example.com", "short")]              // password too short
    [InlineData(null, "a@example.com", "longenough")]
    [InlineData("maria", null, "longenough")]
    [InlineData("maria", "a@example.com", null)]
    public async Task Register_rejects_invalid_input(string? username, string? email, string? password)
    {
        var result = await _auth.RegisterAsync(username, email, password);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_username_or_email()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "s3cret-pass");

        Assert.False((await _auth.RegisterAsync("MARIA", "other@example.com", "s3cret-pass")).Succeeded);
        Assert.False((await _auth.RegisterAsync("other", "maria@example.com", "s3cret-pass")).Succeeded);
    }

    // ---- Sign-in ----

    [Fact]
    public async Task Login_succeeds_with_the_right_password()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "s3cret-pass");

        var result = await _auth.LoginAsync("maria", "s3cret-pass");

        Assert.True(result.Succeeded);
        Assert.Equal("maria", result.Value!.Username);
    }

    [Fact]
    public async Task Login_returns_the_username_as_stored_regardless_of_the_casing_typed()
    {
        await _auth.RegisterAsync("Maria", "maria@example.com", "s3cret-pass");

        var result = await _auth.LoginAsync("  maria ", "s3cret-pass");

        Assert.True(result.Succeeded);
        Assert.Equal("Maria", result.Value!.Username);
    }

    [Fact]
    public async Task Login_fails_with_the_same_message_for_a_wrong_password_and_an_unknown_user()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "s3cret-pass");

        var wrongPassword = await _auth.LoginAsync("maria", "wrong-pass");
        var unknownUser = await _auth.LoginAsync("nobody", "s3cret-pass");

        Assert.False(wrongPassword.Succeeded);
        Assert.False(unknownUser.Succeeded);
        Assert.Equal(wrongPassword.Error, unknownUser.Error);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("maria", "")]
    [InlineData(null, null)]
    public async Task Login_fails_for_blank_credentials(string? username, string? password)
    {
        Assert.False((await _auth.LoginAsync(username, password)).Succeeded);
    }

    [Fact]
    public async Task Login_upgrades_a_legacy_hash_after_a_successful_sign_in()
    {
        var legacy = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("old-password")));
        _users.Add("maria", "maria@example.com", legacy);

        var result = await _auth.LoginAsync("maria", "old-password");

        Assert.True(result.Succeeded);
        Assert.Equal(1, _users.PasswordHashUpdates);
        Assert.StartsWith("PBKDF2$", _users.PasswordHashOf("maria"));
        Assert.True((await _auth.LoginAsync("maria", "old-password")).Succeeded);
    }

    // ---- Password reset ----

    [Fact]
    public async Task Reset_flow_changes_the_password_and_the_link_works_only_once()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "old-password");

        var token = await RequestResetAsync("maria@example.com");

        Assert.True(await _auth.IsResetTokenValidAsync(token));

        var reset = await _auth.ResetPasswordAsync(token, "brand-new-password");
        Assert.True(reset.Succeeded);
        Assert.True((await _auth.LoginAsync("maria", "brand-new-password")).Succeeded);
        Assert.False((await _auth.LoginAsync("maria", "old-password")).Succeeded);

        Assert.False(await _auth.IsResetTokenValidAsync(token));
        Assert.False((await _auth.ResetPasswordAsync(token, "another-password")).Succeeded);
    }

    [Fact]
    public async Task Reset_link_expires_after_one_hour()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "old-password");
        var token = await RequestResetAsync("maria@example.com");

        _clock.Advance(AuthService.ResetTokenLifetime + TimeSpan.FromSeconds(1));

        Assert.False(await _auth.IsResetTokenValidAsync(token));
        Assert.False((await _auth.ResetPasswordAsync(token, "brand-new-password")).Succeeded);
    }

    [Fact]
    public async Task Only_a_hash_of_the_reset_token_is_stored()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "old-password");

        var token = await RequestResetAsync("maria@example.com");

        var stored = _users.TokenHashOf("maria");
        Assert.NotNull(stored);
        Assert.NotEqual(token, stored);
        Assert.DoesNotContain(token, stored);
    }

    [Fact]
    public async Task Requesting_a_reset_for_an_unknown_address_sends_nothing_and_does_not_fail()
    {
        await _auth.RequestPasswordResetAsync("ghost@example.com", t => new EmailMessage("s", $"https://x/{t}"));
        await _auth.RequestPasswordResetAsync("not-an-email", t => new EmailMessage("s", $"https://x/{t}"));

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Requesting_a_reset_does_not_fail_when_the_email_cannot_be_sent()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "old-password");
        _email.ShouldFail = true;

        await _auth.RequestPasswordResetAsync("maria@example.com", t => new EmailMessage("s", $"https://x/{t}"));

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Reset_rejects_a_weak_password_without_consuming_the_token()
    {
        await _auth.RegisterAsync("maria", "maria@example.com", "old-password");
        var token = await RequestResetAsync("maria@example.com");

        Assert.False((await _auth.ResetPasswordAsync(token, "short")).Succeeded);
        Assert.True(await _auth.IsResetTokenValidAsync(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("made-up-token")]
    public async Task Unknown_or_blank_tokens_are_invalid(string? token)
    {
        Assert.False(await _auth.IsResetTokenValidAsync(token));
        Assert.False((await _auth.ResetPasswordAsync(token, "brand-new-password")).Succeeded);
    }

    private async Task<string> RequestResetAsync(string email)
    {
        string? token = null;
        await _auth.RequestPasswordResetAsync(email, t =>
        {
            token = t;
            return new EmailMessage("Reset", $"https://example.com/ResetPassword?token={WebUtility.UrlEncode(t)}");
        });

        Assert.NotNull(token);
        Assert.Single(_email.Sent);
        return token;
    }
}
