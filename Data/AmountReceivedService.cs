using System;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

public class AmountReceivedService
{
    private readonly string _connectionString;

    public AmountReceivedService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Method to add a new amount received record
    public async Task AddAmountReceivedAsync(string username, string name, DateTime date, decimal value, string description)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = @"
            INSERT INTO AmountReceived (Username, Name, Date, Value, Description)
            VALUES (@Username, @Name, @Date, @Value, @Description)";

        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Date", date);
        command.Parameters.AddWithValue("@Value", value);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync();
    }

    // Method to ensure the AmountReceived table exists in the database
    public async Task EnsureDataBaseAndTableAsync()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Remember to change the Database connection information!

        var query = @"
            CREATE TABLE IF NOT EXISTS AmountReceived (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Username VARCHAR(50) NOT NULL,
                Name VARCHAR(25) NOT NULL,
                Date DATE NOT NULL,
                Value DECIMAL(10, 2) NOT NULL,
                Description VARCHAR(75) NOT NULL,
                FOREIGN KEY (Username) REFERENCES Users(Username) ON DELETE CASCADE
            )";

        using var command = new MySqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }
}
