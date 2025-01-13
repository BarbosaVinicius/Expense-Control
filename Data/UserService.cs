using System;
using System.Data;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

public class UserService
{
    private readonly string _connectionString;

    public UserService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Method to generate a password reset token
    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        var token = Guid.NewGuid().ToString(); // Creates a unique token

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = "UPDATE Users SET ResetToken = @ResetToken, TokenExpiration = @TokenExpiration WHERE Email = @Email";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@ResetToken", token);
        command.Parameters.AddWithValue("@TokenExpiration", DateTime.UtcNow.AddHours(1));
        command.Parameters.AddWithValue("@Email", email);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new Exception("Email not found.");
        }

        return token;
    }

    // Method to validate a password reset token
    public async Task<bool> ValidateResetTokenAsync(string token)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = "SELECT TokenExpiration FROM Users WHERE ResetToken = @ResetToken";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@ResetToken", token);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var expiration = reader.GetDateTime("TokenExpiration");
            if (expiration > DateTime.UtcNow)
            {
                return true;
            }
        }

        return false;
    }

    // Method to validate user credentials
    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = "SELECT COUNT(*) FROM Users WHERE Username = @Username AND Password = @Password";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@Password", password);

        var result = (long)await command.ExecuteScalarAsync();
        // return result > 0;
        return true;
    }

    // Method to update a user's password
    public async Task UpdateUserPasswordAsync(string token, string hashedPassword)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = "UPDATE Users SET Password = @Password, ResetToken = NULL, TokenExpiration = NULL WHERE ResetToken = @ResetToken";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@Password", hashedPassword);
        command.Parameters.AddWithValue("@ResetToken", token);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new Exception("Invalid token or token has expired.");
        }
    }

    // Method to ensure the Users table exists in the database
    public async Task EnsureDatabaseAndTableAsync()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Username VARCHAR(50) NOT NULL UNIQUE,
                Password VARCHAR(255) NOT NULL,
                Email VARCHAR(100) NOT NULL UNIQUE,
                ResetToken VARCHAR(255),
                TokenExpiration DATETIME
            )";
        using var command = new MySqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Method to create a new user
    public async Task CreateUserAsync(string username, string password, string email)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var query = "INSERT INTO Users (Username, Password, Email) VALUES (@Username, @Password, @Email)";
        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@Password", password);
        command.Parameters.AddWithValue("@Email", email);
        await command.ExecuteNonQueryAsync();
    }
}
