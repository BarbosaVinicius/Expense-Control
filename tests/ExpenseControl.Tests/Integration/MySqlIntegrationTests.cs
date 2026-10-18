using ExpenseControl.Core.Abstractions;
using ExpenseControl.Core.Common;
using ExpenseControl.Core.Domain;
using ExpenseControl.Core.Services;
using ExpenseControl.Infrastructure.Persistence;
using ExpenseControl.Infrastructure.Security;
using ExpenseControl.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace ExpenseControl.Tests.Integration;

/// <summary>Runs only when EXPENSECONTROL_TEST_CONNECTION points to a MySQL server (see README).</summary>
public sealed class RequiresMySqlFactAttribute : FactAttribute
{
    public const string VariableName = "EXPENSECONTROL_TEST_CONNECTION";

    public RequiresMySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariableName)))
        {
            Skip = $"Set {VariableName} to run the MySQL integration tests.";
        }
    }
}

/// <summary>Each test class instance gets its own throw-away database, created by the real migrations.</summary>
public sealed class MySqlIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = "ec_test_" + Guid.NewGuid().ToString("N");
    private string _serverConnection = string.Empty;
    private MySqlDataSource? _dataSource;
    private MySqlUserRepository _users = null!;
    private MySqlLedgerRepository _ledger = null!;

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable(RequiresMySqlFactAttribute.VariableName);
        if (string.IsNullOrWhiteSpace(connection))
        {
            return; // the tests are skipped
        }

        _serverConnection = connection;
        var builder = new MySqlConnectionStringBuilder(connection) { Database = _databaseName };

        _dataSource = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        await new MigrationRunner(_dataSource, builder.ConnectionString, NullLogger<MigrationRunner>.Instance).RunAsync();

        _users = new MySqlUserRepository(_dataSource);
        _ledger = new MySqlLedgerRepository(_dataSource);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is null)
        {
            return;
        }

        await _dataSource.DisposeAsync();
        await using var server = new MySqlConnection(_serverConnection);
        await server.OpenAsync();
        await using var drop = new MySqlCommand($"DROP DATABASE IF EXISTS `{_databaseName}`", server);
        await drop.ExecuteNonQueryAsync();
    }

    [RequiresMySqlFact]
    public async Task Migrations_are_recorded_and_running_them_again_changes_nothing()
    {
        var builder = new MySqlConnectionStringBuilder(_serverConnection) { Database = _databaseName };
        await new MigrationRunner(_dataSource!, builder.ConnectionString, NullLogger<MigrationRunner>.Instance).RunAsync();

        await using var command = _dataSource!.CreateCommand("SELECT COUNT(*) FROM SchemaVersions");
        Assert.Equal(MigrationRunner.DiscoverScripts().Count, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [RequiresMySqlFact]
    public async Task Users_can_be_created_found_and_have_unique_username_and_email()
    {
        await _users.CreateAsync(new NewUser("Maria", "maria@example.com", "hash"));

        var byName = await _users.FindByUsernameAsync("maria"); // MySQL's collation is case-insensitive
        var byEmail = await _users.FindByEmailAsync("maria@example.com");
        Assert.Equal("Maria", byName!.Username);
        Assert.Equal(byName.Id, byEmail!.Id);
        Assert.Null(await _users.FindByUsernameAsync("nobody"));

        await Assert.ThrowsAsync<DuplicateUserException>(() =>
            _users.CreateAsync(new NewUser("MARIA", "other@example.com", "hash")));
        await Assert.ThrowsAsync<DuplicateUserException>(() =>
            _users.CreateAsync(new NewUser("other", "maria@example.com", "hash")));
    }

    [RequiresMySqlFact]
    public async Task Reset_token_works_once_and_not_after_it_expires()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "old-hash"));
        var user = (await _users.FindByUsernameAsync("maria"))!;
        var now = DateTime.UtcNow;

        await _users.SetPasswordResetTokenAsync(user.Id, "token-hash", now.AddHours(1));
        Assert.True(await _users.IsPasswordResetTokenValidAsync("token-hash", now));
        Assert.False(await _users.IsPasswordResetTokenValidAsync("other-hash", now));
        Assert.False(await _users.IsPasswordResetTokenValidAsync("token-hash", now.AddHours(2)));

        Assert.False(await _users.ResetPasswordAsync("token-hash", "new-hash", now.AddHours(2)));
        Assert.True(await _users.ResetPasswordAsync("token-hash", "new-hash", now));
        Assert.False(await _users.ResetPasswordAsync("token-hash", "again", now));
        Assert.Equal("new-hash", (await _users.FindByUsernameAsync("maria"))!.PasswordHash);
    }

    [RequiresMySqlFact]
    public async Task Ledger_entries_round_trip_in_date_order_and_are_scoped_to_the_user()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "h"));
        await _users.CreateAsync(new NewUser("joao", "joao@example.com", "h"));

        await _ledger.AddAsync(new LedgerEntry(EntryType.Expense, "maria", "Rent", new DateOnly(2025, 3, 5), 1200.50m, "March rent"));
        await _ledger.AddAsync(new LedgerEntry(EntryType.Income, "maria", "Salary", new DateOnly(2025, 3, 1), 3000m, "March salary"));
        await _ledger.AddAsync(new LedgerEntry(EntryType.Expense, "maria", "Old", new DateOnly(2025, 2, 28), 9m, "Outside"));
        await _ledger.AddAsync(new LedgerEntry(EntryType.Income, "joao", "Salary", new DateOnly(2025, 3, 1), 5000m, "Other user"));

        var entries = await _ledger.GetAsync("maria", new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));

        Assert.Collection(entries,
            first =>
            {
                Assert.Equal(new LedgerEntry(EntryType.Income, "maria", "Salary", new DateOnly(2025, 3, 1), 3000m, "March salary"), first with { Id = 0 });
            },
            second =>
            {
                Assert.Equal(new LedgerEntry(EntryType.Expense, "maria", "Rent", new DateOnly(2025, 3, 5), 1200.50m, "March rent"), second with { Id = 0 });
            });
    }

    [RequiresMySqlFact]
    public async Task Deleting_a_user_removes_their_entries()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "h"));
        await _ledger.AddAsync(new LedgerEntry(EntryType.Expense, "maria", "Rent", new DateOnly(2025, 3, 5), 10m, "x"));

        await using var delete = _dataSource!.CreateCommand("DELETE FROM Users WHERE Username = 'maria'");
        await delete.ExecuteNonQueryAsync();

        Assert.Empty(await _ledger.GetAsync("maria", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
    }

    [RequiresMySqlFact]
    public async Task Ledger_entries_can_be_read_back_by_id_updated_and_deleted_with_their_category()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "h"));
        await _users.CreateAsync(new NewUser("joao", "joao@example.com", "h"));

        var id = await _ledger.AddAsync(new LedgerEntry(EntryType.Expense, "maria", "Lunch", new DateOnly(2025, 3, 5), 25m, "Office", "food"));
        Assert.True(id > 0);

        var stored = (await _ledger.GetByIdAsync("maria", EntryType.Expense, id))!;
        Assert.Equal(("Lunch", 25m, "food", id), (stored.Name, stored.Value, stored.Category, stored.Id));
        Assert.Null(await _ledger.GetByIdAsync("joao", EntryType.Expense, id));      // someone else's entry
        Assert.Null(await _ledger.GetByIdAsync("maria", EntryType.Income, id));      // other table

        Assert.True(await _ledger.UpdateAsync(stored with { Name = "Dinner", Value = 40.5m, Category = "leisure", Date = new DateOnly(2025, 3, 6) }));
        Assert.True(await _ledger.UpdateAsync(stored with { Name = "Dinner", Value = 40.5m, Category = "leisure", Date = new DateOnly(2025, 3, 6) })); // unchanged values still count as found
        Assert.False(await _ledger.UpdateAsync(stored with { Id = id + 100 }));
        Assert.False(await _ledger.UpdateAsync(stored with { Username = "joao" }));

        var updated = (await _ledger.GetByIdAsync("maria", EntryType.Expense, id))!;
        Assert.Equal(("Dinner", 40.5m, "leisure", new DateOnly(2025, 3, 6)), (updated.Name, updated.Value, updated.Category, updated.Date));

        Assert.False(await _ledger.DeleteAsync("joao", EntryType.Expense, id));
        Assert.True(await _ledger.DeleteAsync("maria", EntryType.Expense, id));
        Assert.False(await _ledger.DeleteAsync("maria", EntryType.Expense, id));
        Assert.Null(await _ledger.GetByIdAsync("maria", EntryType.Expense, id));
    }

    [RequiresMySqlFact]
    public async Task Budgets_are_upserted_listed_per_user_and_deleted()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "h"));
        await _users.CreateAsync(new NewUser("joao", "joao@example.com", "h"));
        var budgets = new MySqlBudgetRepository(_dataSource!);

        await budgets.SetAsync("maria", new Budget("food", 500m));
        await budgets.SetAsync("maria", new Budget("food", 650.25m));
        await budgets.SetAsync("maria", new Budget("housing", 1000m));
        await budgets.SetAsync("joao", new Budget("food", 1m));

        Assert.Equal([new Budget("food", 650.25m), new Budget("housing", 1000m)], await budgets.GetAsync("maria"));

        await budgets.DeleteAsync("maria", "food");
        Assert.Equal([new Budget("housing", 1000m)], await budgets.GetAsync("maria"));
        Assert.Single(await budgets.GetAsync("joao"));
    }

    [RequiresMySqlFact]
    public async Task Entries_created_before_categories_existed_read_back_as_other()
    {
        await _users.CreateAsync(new NewUser("maria", "maria@example.com", "h"));

        // An insert that does not mention the column, like the application did before migration 004.
        await using var insert = _dataSource!.CreateCommand(
            "INSERT INTO Expenses (Username, Name, Date, Value, Description) VALUES ('maria', 'Old', '2025-03-01', 5, 'x')");
        await insert.ExecuteNonQueryAsync();

        var entry = Assert.Single(await _ledger.GetAsync("maria", new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 1)));
        Assert.Equal("other", entry.Category);
    }

    [RequiresMySqlFact]
    public async Task Full_sign_up_sign_in_and_password_reset_flow_against_the_real_database()
    {
        var email = new FakeEmailSender();
        var auth = new AuthService(_users, new Pbkdf2PasswordHasher(10), email, TimeProvider.System, NullLogger<AuthService>.Instance);

        Assert.True((await auth.RegisterAsync("maria", "maria@example.com", "first-password")).Succeeded);
        Assert.False((await auth.RegisterAsync("maria", "x@example.com", "first-password")).Succeeded);
        Assert.True((await auth.LoginAsync("maria", "first-password")).Succeeded);
        Assert.False((await auth.LoginAsync("maria", "wrong-password")).Succeeded);

        string? token = null;
        await auth.RequestPasswordResetAsync("maria@example.com", t => { token = t; return new EmailMessage("s", "https://x/" + t); });
        Assert.NotNull(token);
        Assert.True((await auth.ResetPasswordAsync(token, "second-password")).Succeeded);
        Assert.True((await auth.LoginAsync("maria", "second-password")).Succeeded);
        Assert.False((await auth.LoginAsync("maria", "first-password")).Succeeded);
    }
}
