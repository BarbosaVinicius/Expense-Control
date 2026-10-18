using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Domain;
using MySqlConnector;

namespace ExpenseControl.Infrastructure.Persistence;

/// <summary>Expenses and amounts received live in separate tables (<c>Expenses</c> and <c>AmountReceived</c>).</summary>
internal sealed class MySqlLedgerRepository(MySqlDataSource dataSource) : ILedgerRepository
{
    public async Task<int> AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"""
            INSERT INTO {TableOf(entry.Type)} (Username, Name, Date, Value, Description, Category)
            VALUES (@Username, @Name, @Date, @Value, @Description, @Category);
            SELECT LAST_INSERT_ID();
            """);
        command.AddParameter("@Username", entry.Username);
        command.AddParameter("@Name", entry.Name);
        command.AddParameter("@Date", entry.Date.ToDateTime(TimeOnly.MinValue));
        command.AddParameter("@Value", entry.Value);
        command.AddParameter("@Description", entry.Description);
        command.AddParameter("@Category", entry.Category);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<LedgerEntry?> GetByIdAsync(
        string username, EntryType type, int id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT Name, Date, Value, Description, Category
            FROM {TableOf(type)}
            WHERE Id = @Id AND Username = @Username
            """);
        command.AddParameter("@Id", id);
        command.AddParameter("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LedgerEntry(
            type,
            username,
            reader.GetString(0),
            DateOnly.FromDateTime(reader.GetDateTime(1)),
            reader.GetDecimal(2),
            reader.GetString(3),
            reader.GetString(4),
            id);
    }

    public async Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"""
            UPDATE {TableOf(entry.Type)}
            SET Name = @Name, Date = @Date, Value = @Value, Description = @Description, Category = @Category
            WHERE Id = @Id AND Username = @Username
            """);
        command.AddParameter("@Name", entry.Name);
        command.AddParameter("@Date", entry.Date.ToDateTime(TimeOnly.MinValue));
        command.AddParameter("@Value", entry.Value);
        command.AddParameter("@Description", entry.Description);
        command.AddParameter("@Category", entry.Category);
        command.AddParameter("@Id", entry.Id);
        command.AddParameter("@Username", entry.Username);

        // Not the number of changed rows: saving without changes must still count as found.
        return await ExistsAfterAsync(command, entry, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string username, EntryType type, int id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"DELETE FROM {TableOf(type)} WHERE Id = @Id AND Username = @Username");
        command.AddParameter("@Id", id);
        command.AddParameter("@Username", username);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetAsync(
        string username, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT 'Expense' AS Kind, Id, Name, Date, Value, Description, Category
            FROM Expenses
            WHERE Username = @Username AND Date BETWEEN @Start AND @End
            UNION ALL
            SELECT 'Income' AS Kind, Id, Name, Date, Value, Description, Category
            FROM AmountReceived
            WHERE Username = @Username AND Date BETWEEN @Start AND @End
            ORDER BY Date, Kind, Id
            """);
        command.AddParameter("@Username", username);
        command.AddParameter("@Start", start.ToDateTime(TimeOnly.MinValue));
        command.AddParameter("@End", end.ToDateTime(TimeOnly.MinValue));

        var entries = new List<LedgerEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new LedgerEntry(
                Enum.Parse<EntryType>(reader.GetString(0)),
                username,
                reader.GetString(2),
                DateOnly.FromDateTime(reader.GetDateTime(3)),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(1)));
        }

        return entries;
    }

    // MySQL reports 0 affected rows when an UPDATE sets the values a row already has, so existence is checked separately.
    private async Task<bool> ExistsAfterAsync(
        System.Data.Common.DbCommand update, LedgerEntry entry, CancellationToken cancellationToken)
    {
        if (await update.ExecuteNonQueryAsync(cancellationToken) > 0)
        {
            return true;
        }

        return await GetByIdAsync(entry.Username, entry.Type, entry.Id, cancellationToken) is not null;
    }

    private static string TableOf(EntryType type) => type switch
    {
        EntryType.Expense => "Expenses",
        EntryType.Income => "AmountReceived",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown entry type.")
    };
}
