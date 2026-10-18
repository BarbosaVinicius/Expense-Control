using ExpenseControl.Core.Domain;

namespace ExpenseControl.Core.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <exception cref="DuplicateUserException">The username or e-mail is already registered.</exception>
    Task CreateAsync(NewUser user, CancellationToken cancellationToken = default);

    Task UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>Stores the hash of a password reset token, replacing any previous one.</summary>
    Task SetPasswordResetTokenAsync(int userId, string tokenHash, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task<bool> IsPasswordResetTokenValidAsync(string tokenHash, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Sets the new password and consumes the token. Returns false if the token is unknown or expired.</summary>
    Task<bool> ResetPasswordAsync(string tokenHash, string newPasswordHash, DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed class DuplicateUserException : Exception
{
    public DuplicateUserException()
        : base("A user with the same username or e-mail already exists.")
    {
    }

    public DuplicateUserException(Exception innerException)
        : base("A user with the same username or e-mail already exists.", innerException)
    {
    }
}
