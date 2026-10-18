using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;
using MySqlConnector;

namespace ExpenseControl.Infrastructure.Persistence;

internal sealed class MySqlBudgetRepository(MySqlDataSource dataSource) : IBudgetRepository
{
    public async Task<IReadOnlyList<Budget>> GetAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT Category, Amount FROM Budgets WHERE Username = @Username ORDER BY Category");
        command.AddParameter("@Username", username);

        var budgets = new List<Budget>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            budgets.Add(new Budget(reader.GetString(0), reader.GetDecimal(1)));
        }

        return budgets;
    }

    public async Task SetAsync(string username, Budget budget, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO Budgets (Username, Category, Amount) VALUES (@Username, @Category, @Amount)
            ON DUPLICATE KEY UPDATE Amount = VALUES(Amount)
            """);
        command.AddParameter("@Username", username);
        command.AddParameter("@Category", budget.Category);
        command.AddParameter("@Amount", budget.Amount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string username, string category, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "DELETE FROM Budgets WHERE Username = @Username AND Category = @Category");
        command.AddParameter("@Username", username);
        command.AddParameter("@Category", category);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
