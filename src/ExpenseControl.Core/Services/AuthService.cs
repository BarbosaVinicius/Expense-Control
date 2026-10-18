using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Common;
using ExpenseControl.Core.Domain;
using Microsoft.Extensions.Logging;

namespace ExpenseControl.Core.Services;

/// <summary>Registration, sign-in and password recovery. Error values are translation keys (see <see cref="OperationResult"/>).</summary>
public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    private const string InvalidCredentials = "error.invalidCredentials";
    private const string InvalidResetLink = "error.resetLinkInvalid";
    private const string PasswordLength = "error.passwordLength";

    public async Task<OperationResult> RegisterAsync(
        string? username, string? email, string? password, CancellationToken cancellationToken = default)
    {
        username = username?.Trim();
        email = email?.Trim();

        if (!AccountRules.IsValidUsername(username))
        {
            return OperationResult.Failure("error.usernameInvalid", AccountRules.MinUsernameLength, AccountRules.MaxUsernameLength);
        }

        if (!AccountRules.IsValidEmail(email))
        {
            return OperationResult.Failure("error.emailInvalid");
        }

        if (!AccountRules.IsValidPassword(password))
        {
            return OperationResult.Failure(PasswordLength, AccountRules.MinPasswordLength, AccountRules.MaxPasswordLength);
        }

        try
        {
            await users.CreateAsync(new NewUser(username, email, passwordHasher.Hash(password)), cancellationToken);
        }
        catch (DuplicateUserException)
        {
            return OperationResult.Failure("error.accountExists");
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult<AuthenticatedUser>> LoginAsync(
        string? username, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return OperationResult<AuthenticatedUser>.Failure(InvalidCredentials);
        }

        var user = await users.FindByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null)
        {
            // Spend roughly the same time as a real verification so response times do not reveal which usernames exist.
            passwordHasher.Hash(password);
            return OperationResult<AuthenticatedUser>.Failure(InvalidCredentials);
        }

        var verification = passwordHasher.Verify(password, user.PasswordHash);
        if (verification == PasswordVerificationResult.Failed)
        {
            return OperationResult<AuthenticatedUser>.Failure(InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await TryUpgradePasswordHashAsync(user, password, cancellationToken);
        }

        return OperationResult<AuthenticatedUser>.Success(new AuthenticatedUser(user.Username));
    }

    /// <summary>
    /// Sends a reset e-mail to the address if it belongs to an account. Always completes silently, so callers
    /// cannot use it to discover which e-mail addresses are registered.
    /// </summary>
    /// <param name="messageFactory">Builds the e-mail (subject, body and the link with the raw token) in the user's language.</param>
    public async Task RequestPasswordResetAsync(
        string? email, Func<string, EmailMessage> messageFactory, CancellationToken cancellationToken = default)
    {
        email = email?.Trim();
        if (!AccountRules.IsValidEmail(email))
        {
            return;
        }

        var user = await users.FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return;
        }

        var token = PasswordResetToken.Generate();
        var expiresAt = clock.GetUtcNow().UtcDateTime + ResetTokenLifetime;
        await users.SetPasswordResetTokenAsync(user.Id, PasswordResetToken.Hash(token), expiresAt, cancellationToken);

        try
        {
            var message = messageFactory(token);
            await emailSender.SendAsync(user.Email, message.Subject, message.HtmlBody, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not surfaced to the caller on purpose (see summary); operators can find it in the logs.
            logger.LogError(ex, "Could not send the password reset e-mail for user {UserId}.", user.Id);
        }
    }

    public async Task<bool> IsResetTokenValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return await users.IsPasswordResetTokenValidAsync(
            PasswordResetToken.Hash(token), clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public async Task<OperationResult> ResetPasswordAsync(
        string? token, string? newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OperationResult.Failure(InvalidResetLink);
        }

        if (!AccountRules.IsValidPassword(newPassword))
        {
            return OperationResult.Failure(PasswordLength, AccountRules.MinPasswordLength, AccountRules.MaxPasswordLength);
        }

        var changed = await users.ResetPasswordAsync(
            PasswordResetToken.Hash(token),
            passwordHasher.Hash(newPassword),
            clock.GetUtcNow().UtcDateTime,
            cancellationToken);

        return changed ? OperationResult.Success() : OperationResult.Failure(InvalidResetLink);
    }

    private async Task TryUpgradePasswordHashAsync(User user, string password, CancellationToken cancellationToken)
    {
        try
        {
            await users.UpdatePasswordHashAsync(user.Id, passwordHasher.Hash(password), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The sign-in itself succeeded; the upgrade will be retried on the next one.
            logger.LogWarning(ex, "Could not upgrade the password hash of user {UserId}.", user.Id);
        }
    }
}
