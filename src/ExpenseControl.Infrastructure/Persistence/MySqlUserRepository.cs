using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;
using MySqlConnector;

namespace ExpenseControl.Infrastructure.Persistence;

internal sealed class MySqlUserRepository(MySqlDataSource dataSource) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        FindAsync("Username", username, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        FindAsync("Email", email, cancellationToken);

    public async Task CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO Users (Username, Password, Email) VALUES (@Username, @Password, @Email)");
        command.AddParameter("@Username", user.Username);
        command.AddParameter("@Password", user.PasswordHash);
        command.AddParameter("@Email", user.Email);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            throw new DuplicateUserException(ex);
        }
    }

    public async Task UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("UPDATE Users SET Password = @Password WHERE Id = @Id");
        command.AddParameter("@Password", passwordHash);
        command.AddParameter("@Id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetPasswordResetTokenAsync(
        int userId, string tokenHash, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE Users SET ResetToken = @Token, TokenExpiration = @Expiration WHERE Id = @Id");
        command.AddParameter("@Token", tokenHash);
        command.AddParameter("@Expiration", expiresAtUtc);
        command.AddParameter("@Id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsPasswordResetTokenValidAsync(
        string tokenHash, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT 1 FROM Users WHERE ResetToken = @Token AND TokenExpiration > @Now LIMIT 1");
        command.AddParameter("@Token", tokenHash);
        command.AddParameter("@Now", nowUtc);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task<bool> ResetPasswordAsync(
        string tokenHash, string newPasswordHash, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE Users
            SET Password = @Password, ResetToken = NULL, TokenExpiration = NULL
            WHERE ResetToken = @Token AND TokenExpiration > @Now
            """);
        command.AddParameter("@Password", newPasswordHash);
        command.AddParameter("@Token", tokenHash);
        command.AddParameter("@Now", nowUtc);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    // "column" is always one of the two literals above, never user input.
    private async Task<User?> FindAsync(string column, string value, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT Id, Username, Email, Password FROM Users WHERE {column} = @Value LIMIT 1");
        command.AddParameter("@Value", value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new User(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }
}
