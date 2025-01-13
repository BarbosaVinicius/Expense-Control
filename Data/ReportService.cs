using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

public class ReportService
{
    private readonly string _connectionString;

    public ReportService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Definition of GetReportAsync
    public async Task<List<ReportItem>> GetReportAsync(string username, DateTime startDate, DateTime endDate)
    {
        var results = new List<ReportItem>();

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Query for AddExpense

        // Remember to change the Database connection information!

        var expenseQuery = @"
            SELECT 'Add Expense' AS Type, Name, Date, Value, Description
            FROM Expenses
            WHERE Username = @Username AND Date BETWEEN @StartDate AND @EndDate";

        using var expenseCommand = new MySqlCommand(expenseQuery, connection);
        expenseCommand.Parameters.AddWithValue("@Username", username);
        expenseCommand.Parameters.AddWithValue("@StartDate", startDate);
        expenseCommand.Parameters.AddWithValue("@EndDate", endDate);

        using var expenseReader = await expenseCommand.ExecuteReaderAsync();
        while (await expenseReader.ReadAsync())
        {
            results.Add(new ReportItem
            {
                Type = expenseReader.GetString(expenseReader.GetOrdinal("Type")),
                Name = expenseReader.GetString(expenseReader.GetOrdinal("Name")),
                Date = expenseReader.GetDateTime(expenseReader.GetOrdinal("Date")),
                Value = expenseReader.GetDecimal(expenseReader.GetOrdinal("Value")),
                Description = expenseReader.GetString(expenseReader.GetOrdinal("Description"))
            });
        }
        expenseReader.Close();

        // Remember to change the Database connection information!

        // Query for AmountReceived
        var receivedQuery = @"
            SELECT 'Amount Received' AS Type, Name, Date, Value, Description
            FROM AmountReceived
            WHERE Username = @Username AND Date BETWEEN @StartDate AND @EndDate";

        using var receivedCommand = new MySqlCommand(receivedQuery, connection);
        receivedCommand.Parameters.AddWithValue("@Username", username);
        receivedCommand.Parameters.AddWithValue("@StartDate", startDate);
        receivedCommand.Parameters.AddWithValue("@EndDate", endDate);

        using var receivedReader = await receivedCommand.ExecuteReaderAsync();
        while (await receivedReader.ReadAsync())
        {
            results.Add(new ReportItem
            {
                Type = receivedReader.GetString(receivedReader.GetOrdinal("Type")),
                Name = receivedReader.GetString(receivedReader.GetOrdinal("Name")),
                Date = receivedReader.GetDateTime(receivedReader.GetOrdinal("Date")),
                Value = receivedReader.GetDecimal(receivedReader.GetOrdinal("Value")),
                Description = receivedReader.GetString(receivedReader.GetOrdinal("Description"))
            });
        }

        return results;
    }
}

// Definition of the ReportItem class
public class ReportItem
{
    public string Type { get; set; }
    public string Name { get; set; }
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string Description { get; set; }
}
